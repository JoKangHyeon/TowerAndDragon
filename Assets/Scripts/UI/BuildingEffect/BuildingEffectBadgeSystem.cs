using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 인구를 배치하는 건물(생산시설·연구소·타워)마다 지역 효과 표식(UI_BuildingEffectBadge)을 런타임으로
/// 부착하고 갱신한다. 프리팹은 건드리지 않는다 - 이후 추가되는 프리팹에도 자동 적용된다(HealthBarSystem과 동일).
///
/// 타워도 대상인 이유: TerrainUpkeepSystem이 건물 종류를 가리지 않고 IPopulationAllocationTarget만
/// 보므로, 설원·암석에 세운 타워도 배치 인구만큼 나무·돌을 낸다. 사막의 공격속도 페널티까지 합치면
/// 타워가 표식 없이 손해만 보는 상태가 된다.
///
/// 갱신은 항상 전체 재계산이다(diff가 아님). 페널티는 건물 하나가 움직여도 다른 건물의 완화량까지
/// 바꿀 수 있어(TerrainPenaltySystem.HandleBuildingChanged 주석 참고) 부분 갱신이 성립하지 않는다.
/// </summary>
public class BuildingEffectBadgeSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;

    [Tooltip("건물 위에 붙일 표식 프리팹(월드 스페이스).")]
    [SerializeField] private UI_BuildingEffectBadge _badgePrefab;

    [Tooltip("효과 종류별 아이콘·문구 테이블.")]
    [SerializeField] private BuildingEffectIconData _iconData;

    [Tooltip("지역 페널티 조회원. 미연결이면 페널티 표식 없이 운영 중단만 표시한다.")]
    [SerializeField] private TerrainPenaltySystem _terrainPenaltySystem;

    [Tooltip("운영 중단 상태의 갱신 알림원. 미연결이면 새끼용 이탈이 표식에 즉시 반영되지 않는다.")]
    [SerializeField] private BabyDragonBuffSystem _babyDragonBuffSystem;

    [Tooltip("인구 배치 변경 알림원. 유지비 표식이 배치 인구에 비례하므로 필요하다. 미연결이면 라벨이 즉시 갱신되지 않는다.")]
    [SerializeField] private PopulationManager _populationManager;

    private readonly Dictionary<Building, UI_BuildingEffectBadge> _badgesByBuilding = new();

    // 갱신 1회 안에서만 쓰는 재사용 버퍼 - 건물마다 새로 할당하지 않는다.
    private readonly List<BuildingEffectDescriptor> _effectBuffer = new();

    /// <summary>호버 툴팁 등 파생 표시가 같은 판정 결과를 다시 쓰기 위한 조회 지점.</summary>
    public bool TryResolveEffects(
        Building building,
        List<BuildingEffectDescriptor> into,
        out TerrainPenaltyModifiers modifiers)
    {
        modifiers = ResolveModifiers(building);

        if (building == null || into == null || !IsEffectDisplayTarget(building))
        {
            return false;
        }

        BuildingEffectResolver.Collect(building, modifiers, ResolvePopulation(building), into);
        return into.Count > 0;
    }

    public BuildingEffectIconData IconData => _iconData;

    private void OnEnable()
    {
        if (WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
        }

        if (WiringGuard.Optional(_terrainPenaltySystem, nameof(_terrainPenaltySystem), this))
        {
            _terrainPenaltySystem.PenaltiesRecomputed.AddListener(RefreshAll);
        }

        if (WiringGuard.Optional(_babyDragonBuffSystem, nameof(_babyDragonBuffSystem), this))
        {
            // BuffsRecomputed는 필드 초기화가 없어 BabyDragonBuffSystem 자신도 ?.로 발화한다 - 같은 방어를 둔다.
            _babyDragonBuffSystem.BuffsRecomputed?.AddListener(RefreshAll);
        }

        // 건물별 인구 이벤트(FactoryPopulation.OnPopulationChanged)를 하나씩 구독하지 않는 이유:
        // TowerPopulation에는 그런 이벤트가 아예 없고, PopulationManager는 두 종류의 배치·해제를
        // 모두 여기로 모아 알린다. 구독 지점 하나로 생산시설과 타워를 함께 처리한다.
        if (WiringGuard.Optional(_populationManager, nameof(_populationManager), this))
        {
            _populationManager.PopulationChanged?.AddListener(HandlePopulationChanged);
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
        }

        if (_terrainPenaltySystem != null)
        {
            _terrainPenaltySystem.PenaltiesRecomputed.RemoveListener(RefreshAll);
        }

        if (_babyDragonBuffSystem != null)
        {
            _babyDragonBuffSystem.BuffsRecomputed?.RemoveListener(RefreshAll);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged?.RemoveListener(HandlePopulationChanged);
        }
    }

    private void Start()
    {
        AttachToExistingBuildingsAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // 유지비 표식은 배치 인구에 비례하므로 인구가 바뀌면 다시 그린다. 어느 건물이 바뀌었는지는
    // 쓰지 않는다 - 어차피 전체 재계산이다.
    private void HandlePopulationChanged(PopulationState state) => RefreshAll();

    // 이 시스템이 구독을 시작하기 전에 이미 GridMap에 등록된 건물이 있을 수 있다(사전 배치 건물).
    // 모든 오브젝트의 Start가 끝난 뒤(한 프레임 지연) 누락분을 훑어 부착한다.
    private async UniTaskVoid AttachToExistingBuildingsAsync(CancellationToken token)
    {
        await UniTask.Yield(token);

        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            Attach(building);
        }

        RefreshAll();
    }

    private void HandleBuildingAdded(Building building)
    {
        Attach(building);

        // 부착과 PenaltiesRecomputed 발화의 순서는 보장되지 않는다(둘 다 OnBuildingAdded 구독자다).
        // 새로 지은 건물이 첫 프레임부터 옳게 그려지도록 여기서 한 번 더 갱신한다.
        RefreshAll();
    }

    private void HandleBuildingRemoving(Building building)
    {
        // 표식은 건물의 자식이라 건물과 함께 파괴된다 - 여기서는 사전만 정리한다.
        _badgesByBuilding.Remove(building);
    }

    /// <summary>
    /// 표식과 그 설명 툴팁의 대상인지. 대상은 인구를 배치하는 건물(생산시설·연구소·타워)이다 -
    /// 성처럼 지역 페널티를 받지 않는 건물까지 훑으면 빈 표식만 잔뜩 만든다.
    /// 연구소도 산출(RP) 감소와 자재 유지비를 둘 다 받으므로 대상이다.
    ///
    /// 툴팁(BuildingEffectHoverTooltip)도 이 판정을 함께 쓴다. 표식만 걸러내면 표식이 없는 건물에
    /// 툴팁만 뜨고, 이름 출처가 없어(BuildingEffectTooltipBuilder.ResolveTitle) 제목 없는 툴팁이 된다.
    /// </summary>
    public static bool IsEffectDisplayTarget(Building building) => building is Factory or Tower or ResearchLab;

    private void Attach(Building building)
    {
        if (!IsEffectDisplayTarget(building) ||
            !WiringGuard.Require(_badgePrefab, nameof(_badgePrefab), this))
        {
            return;
        }

        if (_badgesByBuilding.ContainsKey(building))
        {
            return;
        }

        // 건물 이동 등으로 재등록되면 이미 붙어 있을 수 있다 - 중복 부착 방지.
        UI_BuildingEffectBadge badge = building.GetComponentInChildren<UI_BuildingEffectBadge>(true);

        if (badge == null)
        {
            // 소유 스프라이트는 표식을 붙이기 전에 찾는다 - 붙인 뒤에 찾으면 표식 안쪽을 함께 훑는다.
            SpriteRenderer ownerRenderer = building.GetComponentInChildren<SpriteRenderer>();
            badge = Instantiate(_badgePrefab, building.transform);
            badge.Bind(ownerRenderer);
        }

        _badgesByBuilding[building] = badge;
    }

    private void RefreshAll()
    {
        foreach (KeyValuePair<Building, UI_BuildingEffectBadge> pair in _badgesByBuilding)
        {
            Building building = pair.Key;
            UI_BuildingEffectBadge badge = pair.Value;

            if (building == null || badge == null)
            {
                continue;
            }

            BuildingEffectResolver.Collect(
                building,
                ResolveModifiers(building),
                ResolvePopulation(building),
                _effectBuffer);

            badge.Render(_effectBuffer, _iconData);
        }
    }

    // 페널티 시스템이 배선되지 않은 씬(팀원 테스트 씬 등)에서는 중립값으로 동작한다 -
    // 운영 중단 표식만 뜨고 나머지는 뜨지 않는다(IBuildingTerrainPenaltyQuery와 같은 관례).
    private TerrainPenaltyModifiers ResolveModifiers(Building building) =>
        _terrainPenaltySystem != null && building != null
            ? _terrainPenaltySystem.Resolve(building)
            : TerrainPenaltyModifiers.Neutral;

    // 인구를 배치할 수 없는 건물은 null - 그런 건물은 지역 유지비도 내지 않는다.
    private static IPopulationAllocationTarget ResolvePopulation(Building building)
    {
        if (building == null)
        {
            return null;
        }

        return building.TryGetComponent(out IPopulationAllocationTarget target) ? target : null;
    }
}
