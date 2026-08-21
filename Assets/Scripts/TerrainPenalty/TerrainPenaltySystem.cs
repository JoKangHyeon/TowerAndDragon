using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 지역(지형) 페널티의 단일 조회 지점. 건물의 풋프린트 지형 비율 × 테이블 원본 값 ×
/// 외부 완화 배율을 합쳐 건물 하나가 실제로 받는 페널티를 만든다.
///
/// 지연 계산(pull)이라 초기화 순서에 의존하지 않는다 - 구독자가 Awake/OnEnable에서 값을
/// 미리 받아둘 필요가 없고, 첫 호출 시점에 그리드가 완성돼 있기만 하면 된다.
/// 결과는 건물별로 캐싱하고 배치·철거·이동과 완화 소스 변경 시에만 버린다(TowerAttack이
/// 발사할 때마다 조회하므로 매번 풋프린트를 다시 세면 안 된다).
/// </summary>
public class TerrainPenaltySystem : MonoBehaviour, IBuildingTerrainPenaltyQuery
{
    [SerializeField] private GridMap _gridMap;

    [Tooltip("지형별 페널티 수치. 미연결이면 모든 건물이 페널티 없음으로 동작한다.")]
    [SerializeField] private TerrainPenaltyData _penaltyData;

    [Tooltip("페널티를 건물 단위로 완화·무효화하는 소스(새끼용 등)의 합성 지점. 미연결이면 완화 없음.")]
    [SerializeField] private TerrainPenaltyScaleComposite _scaleComposite;

    /// <summary>
    /// 페널티가 다시 계산될 여지가 생겼을 때 발화(건물 배치·철거·이동, 완화 소스 변경).
    /// ResourceForecast 등 파생 표시가 최신 값을 다시 읽게 하는 훅이다.
    /// </summary>
    public UnityEvent PenaltiesRecomputed = new();

    private readonly Dictionary<Building, TerrainPenaltyModifiers> _modifiersByBuilding = new();

    // 계산용 재사용 버퍼 - 건물마다 새로 할당하지 않는다.
    private readonly List<TerrainType> _terrainBuffer = new();
    private readonly Dictionary<TerrainType, float> _exposureBuffer = new();

    private void OnEnable()
    {
        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        _gridMap.OnBuildingAdded.AddListener(HandleBuildingChanged);
        _gridMap.OnBuildingRemoving.AddListener(HandleBuildingChanged);
        _gridMap.OnBuildingMoved.AddListener(HandleBuildingChanged);
    }

    private void OnDisable()
    {
        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingChanged);
        _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingChanged);
        _gridMap.OnBuildingMoved.RemoveListener(HandleBuildingChanged);
    }

    /// <summary>
    /// 완화 소스(새끼용 배치·모드 변경 등)가 바뀌었을 때 호출한다. 캐시를 버리고 알림만 쏘며,
    /// 실제 재계산은 다음 조회 때 일어난다.
    /// </summary>
    public void NotifyScaleChanged() => InvalidateAll();

    public TerrainPenaltyModifiers Resolve(Building building)
    {
        if (building == null)
        {
            return TerrainPenaltyModifiers.Neutral;
        }

        if (_modifiersByBuilding.TryGetValue(building, out TerrainPenaltyModifiers cached))
        {
            return cached;
        }

        TerrainPenaltyModifiers resolved = Compute(building);
        _modifiersByBuilding[building] = resolved;
        return resolved;
    }

    /// <summary>
    /// 아직 배치되지 않은 풋프린트가 받게 될 지역 페널티 - 배치 미리보기 전용이다.
    /// 배치된 건물과 같은 계산(ComputeFromCoords)을 쓰므로 미리보기 값과 실제 값이 갈라지지 않는다.
    ///
    /// 캐시하지 않는다 - 고스트는 커서를 옮길 때마다 다른 풋프린트가 되므로 캐시가 의미가 없고,
    /// 건물별 캐시(_modifiersByBuilding)를 미리보기로 오염시키면 안 된다.
    /// worldPosition은 그 자리에 실제로 지었을 때 건물이 놓일 좌표여야 한다
    /// (완화 판정이 그 점 기준 타원이므로 - GridMap.CreatePlacedInstance와 같은 식).
    /// </summary>
    public TerrainPenaltyModifiers ResolvePreview(IReadOnlyList<Vector3Int> footprint, Vector3 worldPosition) =>
        ComputeFromCoords(footprint, worldPosition);

    // 철거된 건물은 캐시에서 지우고, 배치·이동은 지형 구성이 달라지므로 전부 버린다.
    // 개별 건물만 버리지 않는 이유: 완화 소스가 위치 기반(새끼용 반경)이면 한 건물의 이동이
    // 다른 건물의 완화량까지 바꿀 수 있다.
    private void HandleBuildingChanged(Building building) => InvalidateAll();

    private void InvalidateAll()
    {
        _modifiersByBuilding.Clear();
        PenaltiesRecomputed?.Invoke();
    }

    private TerrainPenaltyModifiers Compute(Building building) =>
        _gridMap != null
            ? ComputeFromCoords(_gridMap.GetFootprintCoords(building), building.transform.position)
            : TerrainPenaltyModifiers.Neutral;

    // 배치된 건물과 배치 미리보기가 공유하는 본체. 완화 배율은 건물이 아니라 좌표로 조회하므로
    // 둘의 차이는 "풋프린트를 어디서 얻었는가" 하나뿐이다.
    private TerrainPenaltyModifiers ComputeFromCoords(
        IReadOnlyList<Vector3Int> footprint,
        Vector3 worldPosition)
    {
        if (_gridMap == null || _penaltyData == null || footprint == null)
        {
            return TerrainPenaltyModifiers.Neutral;
        }

        _terrainBuffer.Clear();

        foreach (Vector3Int coord in footprint)
        {
            _terrainBuffer.Add(_gridMap.GetTerrainType(coord));
        }

        BuildingTerrainExposure.Accumulate(_terrainBuffer, _exposureBuffer);

        if (_exposureBuffer.Count == 0)
        {
            return TerrainPenaltyModifiers.Neutral;
        }

        float yieldReduction = 0f;
        float attackSpeedReduction = 0f;
        float woodUpkeep = 0f;
        float stoneUpkeep = 0f;

        foreach (KeyValuePair<TerrainType, float> exposure in _exposureBuffer)
        {
            TerrainType terrain = exposure.Key;
            float weight = exposure.Value;
            TerrainPenaltyEntry penalty = _penaltyData.Resolve(terrain);

            yieldReduction += penalty.YieldReductionRatio * weight *
                GetScale(worldPosition, terrain, TerrainPenaltyKind.Yield);

            attackSpeedReduction += penalty.TowerAttackSpeedReductionRatio * weight *
                GetScale(worldPosition, terrain, TerrainPenaltyKind.TowerAttackSpeed);

            woodUpkeep += penalty.WoodUpkeepPerPopulation * weight *
                GetScale(worldPosition, terrain, TerrainPenaltyKind.WoodUpkeep);

            stoneUpkeep += penalty.StoneUpkeepPerPopulation * weight *
                GetScale(worldPosition, terrain, TerrainPenaltyKind.StoneUpkeep);
        }

        // 완화 소스가 페널티를 심화(배율 1 초과)시킬 수 있으므로 생산량·공격속도가 음수로
        // 뒤집히지 않도록 0에서 자른다.
        return new TerrainPenaltyModifiers(
            Mathf.Max(0f, 1f - yieldReduction),
            Mathf.Max(0f, 1f - attackSpeedReduction),
            Mathf.Max(0f, woodUpkeep),
            Mathf.Max(0f, stoneUpkeep));
    }

    private float GetScale(Vector3 worldPosition, TerrainType terrain, TerrainPenaltyKind kind) =>
        _scaleComposite != null
            ? _scaleComposite.GetPenaltyScale(worldPosition, terrain, kind)
            : 1f;
}
