using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 새끼용 주변 생산건물(Factory)에 생산량 배율 버프를 적용한다.
/// 버프 반경은 BabyDragonData.BuffRadius(공격 사거리와 무관한 독립 값)를 쓴다 - Attack을
/// 아예 안 붙인 버프 전용 개체(예: 생명 속성)도 버프를 낼 수 있어야 하기 때문이다.
/// TowerAttack과 동일한 IsometricMath 타원 판정을 좌표만으로 수행하므로 생산건물에
/// 콜라이더가 없어도 동작한다.
/// 배치·철거·이동(GridMap.OnBuildingAdded/OnBuildingRemoving/OnBuildingMoved) 즉시,
/// 그리고 매일 밤(OnNightEnd) 전체를 다시 계산한다 - Factory.GetCurrentYield가
/// "다음 정산에서 실제로 들어올 양"을 상시 정확히 보여주려면 배율이 실시간으로 맞아야 한다.
/// </summary>
public class BabyDragonBuffSystem : MonoBehaviour, IConstructionOverrideQuery, ITerrainPenaltyScaleQuery
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private CycleManager _cycleManager;

    // 새끼용 지역형(B 슬롯) 노드 해금 시 추가되는 보너스 - 미해금이면 0을 반환해 기존 동작과 같다.
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [Tooltip("새끼용의 지역 페널티 완화를 등록할 합성 지점. 미연결이면 완화가 반영되지 않는다 - " +
        "지형 페널티를 일부러 쓰지 않는 씬(튜토리얼)이 그렇다.")]
    [WiringOptional]
    [SerializeField] private TerrainPenaltyScaleComposite _terrainPenaltyScaleComposite;
    [Tooltip("완화 결과가 바뀌었음을 알릴 대상. 미연결이면 알리지 않는다 - " +
        "지형 페널티를 일부러 쓰지 않는 씬(튜토리얼)이 그렇다.")]
    [WiringOptional]
    [SerializeField] private TerrainPenaltySystem _terrainPenaltySystem;


    // 새끼용/생산시설 배치가 바뀌어 배율이 다시 확정될 때마다 발화 - ResourceForecast 등
    // 파생 UI가 OnBuildingAdded/OnBuildingRemoving과 같은 프레임에서 순서에 의존하지 않고
    // 최신 배율을 읽을 수 있게 하기 위한 훅이다.
    public UnityEvent BuffsRecomputed;

    private readonly List<BabyDragonTower> _babyDragons = new();
    private readonly List<Factory> _factories = new();

    // 이번 재계산에서 건설 제한이 해제된 셀들 - IConstructionOverrideQuery 구현에 쓴다.
    private readonly HashSet<Vector3Int> _unlockedConstructionCells = new();


    // (생산시설, 자원) 단위로 배율을 누적한다 - 슬라임 농장처럼 한 시설이 여러 자원을
    // 생산할 때 버프 대상 자원만 골라 곱해야 하기 때문이다.
    private readonly Dictionary<(Factory, ResourceType), float> _multiplierByFactoryResource = new();

    // Factory에 넘길 자원별 배율을 담는 재사용 버퍼 - 시설마다 새로 할당하지 않는다.
    private readonly Dictionary<ResourceType, float> _pushBuffer = new();


    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.ConstructionOverrideQuery = this;
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
            _gridMap.OnBuildingMoved.AddListener(HandleBuildingMoved);
        }

        if (_terrainPenaltyScaleComposite != null)
        {
            _terrainPenaltyScaleComposite.Register(this);
        }

        if (_cycleManager != null)
        {
            // 생산 정산(Factory.OnSettlement)은 OnDayStart, 먹이 지불은 그 뒤 단계인
            // OnDayStartUpkeep에서 실행되므로(Factory.cs:84), 버프는 둘보다 앞서 확정해야
            // 한다. OnNightEnd -> StartDay -> OnDayStart 순서가 같은 호출 스택 안에서
            // 보장되므로(CycleManager.cs:52-56) OnNightEnd에서 재계산한다.
            _cycleManager.OnNightEnd.AddListener(RecomputeAllOnNightEnd);
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null && ReferenceEquals(_gridMap.ConstructionOverrideQuery, this))
        {
            _gridMap.ConstructionOverrideQuery = null;
        }

        if (_terrainPenaltyScaleComposite != null)
        {
            _terrainPenaltyScaleComposite.Unregister(this);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.RemoveListener(RecomputeAllOnNightEnd);
        }
    }

    private void HandleBuildingAdded(Building building)
    {
        if (building is BabyDragonTower babyDragon && !_babyDragons.Contains(babyDragon))
        {
            _babyDragons.Add(babyDragon);
            babyDragon.ModeChanged.AddListener(RecomputeAll);
            RecomputeAll();
        }
        else if (building is Factory factory && !_factories.Contains(factory))
        {
            // 이 시점의 배율은 RecomputeAll이 즉시 확정한다 - 기존 새끼용 반경 안에 새로
            // 지어지면 배치 직후부터 버프가 반영된다(다음 아침 실제 정산과 동일한 값).
            _factories.Add(factory);
            RecomputeAll();
        }
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (building is BabyDragonTower babyDragon)
        {
            babyDragon.ModeChanged.RemoveListener(RecomputeAll);
            _babyDragons.Remove(babyDragon);
            RecomputeAll();
        }
        else if (building is Factory factory)
        {
            _factories.Remove(factory);
        }
    }

    private void HandleBuildingMoved(Building building)
    {
        // BabyDragonTower든 Factory든 위치가 바뀌면 범위 판정이 달라지므로 무조건 재계산한다.
        if (building is BabyDragonTower or Factory)
        {
            RecomputeAll();
        }
    }

    // CanOperate(먹이 상태)는 그날 아침 BabyDragonFeedingSystem.OnDayStartUpkeep이 갱신한 값이다.
    // 즉 밤 시점(OnNightEnd)의 재계산은 "그날 아침 먹었는지"를 반영하고, 다음 날 아침 정산에
    // 쓰인다 - 하루 지연이지만 순환 의존은 아니다(먹이 계산에는 버프가 관여하지 않음).
    // 배치/철거/이동에 의한 재계산은 CanOperate가 낮 동안 바뀌지 않으므로 같은 값을 그대로 쓴다.
    private void RecomputeAllOnNightEnd(int _) => RecomputeAll();

    private void RecomputeAll()
    {
        _multiplierByFactoryResource.Clear();

        foreach (Factory factory in _factories)
        {
            foreach (ResourceType resourceType in factory.EnumerateProducedResourceTypes())
            {
                _multiplierByFactoryResource[(factory, resourceType)] = Factory.NEUTRAL_YIELD_MULTIPLIER;
            }
        }

        foreach (BabyDragonTower babyDragon in _babyDragons)
        {
            if (!babyDragon.CanOperate ||
                babyDragon.DragonData == null ||
                babyDragon.Mode != BabyDragonMode.Buff)
            {
                continue;
            }

            ApplyBuffFrom(babyDragon, _multiplierByFactoryResource);
        }

        foreach (Factory factory in _factories)
        {
            _pushBuffer.Clear();

            foreach (ResourceType resourceType in factory.EnumerateProducedResourceTypes())
            {
                float multiplier = _multiplierByFactoryResource[(factory, resourceType)];
                _pushBuffer[resourceType] = multiplier;
                Debug.Log($"[BabyDragonBuffSystem] 최종 배율 → {factory.name} ({resourceType}): ×{multiplier}");
            }

            factory.SetAreaYieldMultipliers(_pushBuffer);
        }

        RecomputeConstructionUnlocks();

        // 완화 자체는 조회 시점에 계산하지만(GetPenaltyScale), TerrainPenaltySystem은 건물별로 캐싱하므로
        // 새끼용이 움직이거나 모드가 바뀌면 그 캐시를 버리라고 알려야 한다.
        _terrainPenaltySystem?.NotifyScaleChanged();

        BuffsRecomputed?.Invoke();
    }

    // 새끼용 지역형 노드(KinBuffRadiusEffectSO)의 반경 보너스를 반영한 실효 버프 반경.
    // BuffRadius를 직접 읽는 곳이 셋(생산 버프·건설 해제·지형 페널티 완화)이라 반드시 여기로 모은다 -
    // 하나만 늘리면 생산 범위와 건설 해제 범위가 눈에 보이게 어긋난다.
    public float GetEffectiveBuffRadius(BabyDragonTower babyDragon)
    {
        float baseRadius = babyDragon.DragonData.BuffRadius;

        float bonus = _dragonTreeManager != null
            ? _dragonTreeManager.GetKinBuffRadiusBonusRatio(babyDragon.DragonData.DragonType)
            : 0f;

        return baseRadius * (1f + bonus);
    }

    private void RecomputeConstructionUnlocks()
    {
        _unlockedConstructionCells.Clear();

        foreach(BabyDragonTower babyDragon in _babyDragons)
        {
            if (!babyDragon.CanOperate ||
                babyDragon.DragonData == null ||
                babyDragon.Mode != BabyDragonMode.Buff)
            {
                continue;
            }

            CollectUnlockedCells (babyDragon);
        }

        SuspendBuildingsOutsideUnlock();
    }

    private void CollectUnlockedCells(BabyDragonTower babyDragon)
    {
        float radius = GetEffectiveBuffRadius(babyDragon);
        IReadOnlyList<TerrainType> unlockTerrains = babyDragon.DragonData.ConstructionUnlockTerrains;

        if (radius <= 0f || unlockTerrains == null || unlockTerrains.Count == 0)
        {
            return;
        }

        float radiusY = radius * IsometricMath.RADIUS_Y_RATIO;
        Vector3 center = babyDragon.transform.position;

        foreach (Vector3Int coord in _gridMap.EnumerateAllCoords())
        {
            TerrainType terrain = _gridMap.GetTerrainType(coord);
            bool isTargetTerrain = false;

            for (int i = 0; i < unlockTerrains.Count; i++)
            {
                if (unlockTerrains[i] == terrain)
                {
                    isTargetTerrain = true;
                    break;
                }
            }

            if (!isTargetTerrain)
            {
                continue;
            }

            Vector3 cellWorldPos = _gridMap.ConvertGridToWorld(coord); // 실제로는 _gridMap.ConvertGridToWorld(coord)

            if (IsometricMath.IsWithinEllipse(cellWorldPos, center, radius, radiusY))
            {
                _unlockedConstructionCells.Add(coord);
            }
        }
    }

    // 이 새끼용이 그 위치의 지형 페널티를 무효화하는가. 판정 기준은 생산 버프·건설 해제와 같은
    // 타원 반경이다(GetEffectiveBuffRadius 주석 참고).
    private bool IsPenaltyMitigatedBy(
        BabyDragonTower babyDragon,
        Vector3 worldPosition,
        TerrainType terrain)
    {
        if (!babyDragon.CanOperate ||
            babyDragon.DragonData == null ||
            babyDragon.Mode != BabyDragonMode.Buff)
        {
            return false;
        }

        float radius = GetEffectiveBuffRadius(babyDragon);
        IReadOnlyList<TerrainType> mitigationTerrains = babyDragon.DragonData.PenaltyMitigationTerrains;

        if (radius <= 0f || mitigationTerrains == null || mitigationTerrains.Count == 0)
        {
            return false;
        }

        bool isTargetTerrain = false;

        for (int i = 0; i < mitigationTerrains.Count; i++)
        {
            if (mitigationTerrains[i] == terrain)
            {
                isTargetTerrain = true;
                break;
            }
        }

        if (!isTargetTerrain)
        {
            return false;
        }

        return IsometricMath.IsWithinEllipse(
            worldPosition,
            babyDragon.transform.position,
            radius,
            radius * IsometricMath.RADIUS_Y_RATIO);
    }

    // 화산 지대에 지어진 건물 중 지금 해제 범위를 벗어난 것을 전부 정지시키고 인구를 회수한다.
    // 매번 전체를 다시 판정한다(diff가 아님) - 새끼용이 철거·이동·모드전환 등 어떤 경로로 사라져도
    // 같은 로직 한 곳으로 처리되고, 세이브 로드 직후에도 그대로 맞는 상태가 나온다.
    private void SuspendBuildingsOutsideUnlock()
    {
        foreach (Building building in _gridMap.Buildings)
        {
            // 운영 중단은 인구 배치를 막는 상태가 전부다(Building.IsSuspended 주석). 인구를 배치할 수
            // 없는 건물은 정지시켜도 실제로 달라지는 게 없고 표식·툴팁만 잘못 뜬다 - 성은 3x3 전체가
            // 통행로(건설 불가 지형) 위라 여기에 그대로 걸려 "운영 중단" 툴팁이 떴다.
            if (!building.TryGetComponent(out IPopulationAllocationTarget population))
            {
                continue;
            }

            IReadOnlyList<Vector3Int> footprint = _gridMap.GetFootprintCoords(building);

            if (footprint.Count == 0)
            {
                continue;
            }

            bool needsUnlock = false;
            bool stillUnlocked = true;

            foreach (Vector3Int coord in footprint)
            {
                if (!_gridMap.IsNaturallyConstructible(coord))
                {
                    needsUnlock = true;

                    if (!_unlockedConstructionCells.Contains(coord))
                    {
                        stillUnlocked = false;
                    }
                }
            }

            if (!needsUnlock)
            {
                continue;
            }

            building.SetSuspended(!stillUnlocked);

            if (!stillUnlocked)
            {
                population.TryUnassign(population.AssignedPopulation);
            }
        }
    }


    private void ApplyBuffFrom(
        BabyDragonTower babyDragon,
        Dictionary<(Factory, ResourceType), float> multiplierByFactoryResource)
    {
        float radius = GetEffectiveBuffRadius(babyDragon);
        ResourceType targetResources = babyDragon.DragonData.BuffTargetResources;

        // 반경이 없거나(예: 얼음/불/시간 - 생산량 버프가 아닌 별개 지역 효과를 쓴다) 대상 자원이
        // 지정되지 않았으면 계산을 건너뛴다.
        if (radius <= 0f || targetResources == ResourceType.None)
        {
            return;
        }

        float radiusY = radius * IsometricMath.RADIUS_Y_RATIO;
        Vector3 center = babyDragon.transform.position;

        // 시설마다 달라지지 않는 값이라 루프 밖에서 한 번만 구한다.
        float babyDragonMultiplier = GetEffectiveYieldMultiplier(babyDragon.DragonData);

        foreach (Factory factory in _factories)
        {
            if (!IsometricMath.IsWithinEllipse(factory.transform.position, center, radius, radiusY))
            {
                Debug.Log($"[BabyDragonBuffSystem] {factory.name}: 범위 밖 → 버프 없음");
                continue;
            }

            foreach (ResourceType resourceType in factory.EnumerateProducedResourceTypes())
            {
                // EnumerateProducedResourceTypes는 단일 비트만 내놓으므로 마스크 교집합 검사로
                // 이 새끼용이 버프하는 자원인지 판정한다(슬라임 농장처럼 여러 자원을 생산해도
                // 대상 자원만 골라 곱한다).
                if ((targetResources & resourceType) == 0)
                {
                    continue;
                }

                var key = (factory, resourceType);
                float before = multiplierByFactoryResource[key];
                multiplierByFactoryResource[key] *= babyDragonMultiplier;
                Debug.Log($"[BabyDragonBuffSystem] {factory.name} ({resourceType}): 범위 안 → ×{before} → ×{multiplierByFactoryResource[key]} (새끼용 배율 ×{babyDragonMultiplier})");
            }
        }
    }

    /// <summary>
    /// 이 데이터의 새끼용이 생산량에 실제로 곱하는 배율(혈족 강화 포함). 버프 적용과 UI 표시가
    /// 같은 값을 쓰도록 계산 경로를 여기 하나로 모은다.
    /// </summary>
    public float GetEffectiveYieldMultiplier(BabyDragonData data) =>
        BabyDragonBuffFormula.ResolveYieldMultiplier(data, GetKinBonusRatio(data));

    private float GetKinBonusRatio(BabyDragonData data) =>
        _dragonTreeManager != null && data != null
            ? _dragonTreeManager.GetKinAreaYieldBonusRatio(data.DragonType)
            : BabyDragonBuffFormula.NO_KIN_BONUS_RATIO;

    /// <summary>
    /// 이 새끼용 한 마리가 지금 버프하고 있는 (생산시설, 자원) 쌍을 모은다. 표시 전용 조회다.
    ///
    /// 재계산 결과(_multiplierByFactoryResource)는 모든 새끼용의 배율을 곱해 누적한 값이라 거기서
    /// "이 마리의 몫"만 되돌릴 수 없다. 그래서 같은 판정을 이 마리 기준으로 한 번 더 돈다 -
    /// 커서를 올리고 있는 동안에만 호출되므로 생산시설 수만큼의 비용은 문제되지 않는다.
    /// </summary>
    public void CollectBuffTargets(BabyDragonTower babyDragon, List<BabyDragonBuffTarget> into)
    {
        if (into == null)
        {
            return;
        }

        into.Clear();

        if (babyDragon == null ||
            !babyDragon.CanOperate ||
            babyDragon.Mode != BabyDragonMode.Buff ||
            !BabyDragonBuffFormula.HasYieldBuff(babyDragon.DragonData))
        {
            return;
        }

        BabyDragonData data = babyDragon.DragonData;
        float radius = GetEffectiveBuffRadius(babyDragon);
        float radiusY = radius * IsometricMath.RADIUS_Y_RATIO;
        Vector3 center = babyDragon.transform.position;

        foreach (Factory factory in _factories)
        {
            if (!IsometricMath.IsWithinEllipse(
                    factory.transform.position, center, radius, radiusY))
            {
                continue;
            }

            foreach (ResourceType resourceType in factory.EnumerateProducedResourceTypes())
            {
                if ((data.BuffTargetResources & resourceType) == 0)
                {
                    continue;
                }

                into.Add(new BabyDragonBuffTarget(factory, resourceType));
            }
        }
    }

    public bool IsConstructionAllowed(Vector3Int coord, TerrainType terrain)
    {
        return _unlockedConstructionCells.Contains(coord);
    }

    // kind는 구분하지 않는다 - (위치,지형)이 완화 대상이면 4종 페널티를 전부 0으로 만든다.
    // 사막은 어차피 Yield·TowerAttackSpeed만 값이 있어(WoodUpkeep·StoneUpkeep은 원래 0) 결과는 같다.
    //
    // 집합을 미리 구워두지 않고 조회 시점에 판정한다 - 배치 미리보기의 고스트처럼 아직 Building이
    // 없는 대상도 배치된 건물과 정확히 같은 경로로 물어보게 하기 위함이다. 호출자
    // (TerrainPenaltySystem)가 건물별로 캐싱하므로 새끼용 수만큼의 이 순회는 문제되지 않는다.
    public float GetPenaltyScale(Vector3 worldPosition, TerrainType terrain, TerrainPenaltyKind kind)
    {
        foreach (BabyDragonTower babyDragon in _babyDragons)
        {
            if (IsPenaltyMitigatedBy(babyDragon, worldPosition, terrain))
            {
                return 0f;
            }
        }

        return 1f;
    }
}
