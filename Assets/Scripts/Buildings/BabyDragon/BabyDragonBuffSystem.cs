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
public class BabyDragonBuffSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private CycleManager _cycleManager;

    // 새끼용 지역형(B 슬롯) 노드 해금 시 추가되는 보너스 - 미해금이면 0을 반환해 기존 동작과 같다.
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    // 새끼용/생산시설 배치가 바뀌어 배율이 다시 확정될 때마다 발화 - ProductionForecast 등
    // 파생 UI가 OnBuildingAdded/OnBuildingRemoving과 같은 프레임에서 순서에 의존하지 않고
    // 최신 배율을 읽을 수 있게 하기 위한 훅이다.
    public UnityEvent BuffsRecomputed;

    private readonly List<BabyDragonTower> _babyDragons = new();
    private readonly List<Factory> _factories = new();

    // 매 재계산마다 새로 할당하지 않도록 재사용한다 - 재계산 빈도가 하루 1회(OnNightEnd)에서
    // 배치/철거 조작마다로 늘어났기 때문이다(GridMap._chunkYieldBuffer와 같은 관례).
    private readonly Dictionary<Factory, float> _multiplierByFactory = new();

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
            _gridMap.OnBuildingMoved.AddListener(HandleBuildingMoved);
        }

        if (_cycleManager != null)
        {
            // Factory.OnSettlement가 OnDayStart에서 실행되므로(Factory.cs:36), 그 전에
            // 버프를 확정해야 한다. OnNightEnd -> StartDay -> OnDayStart 순서가 같은 호출
            // 스택 안에서 보장되므로(CycleManager.cs:52-56) OnNightEnd에서 재계산한다.
            _cycleManager.OnNightEnd.AddListener(RecomputeAllOnNightEnd);
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
            _gridMap.OnBuildingMoved.RemoveListener(HandleBuildingMoved);
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

    // CanOperate(먹이 상태)는 그날 아침 BabyDragonFeedingSystem.OnDayStart가 갱신한 값이다.
    // 즉 밤 시점(OnNightEnd)의 재계산은 "그날 아침 먹었는지"를 반영하고, 다음 날 아침 정산에
    // 쓰인다 - 하루 지연이지만 순환 의존은 아니다(먹이 계산에는 버프가 관여하지 않음).
    // 배치/철거/이동에 의한 재계산은 CanOperate가 낮 동안 바뀌지 않으므로 같은 값을 그대로 쓴다.
    private void RecomputeAllOnNightEnd(int _) => RecomputeAll();

    private void RecomputeAll()
    {
        _multiplierByFactory.Clear();

        foreach (Factory factory in _factories)
        {
            _multiplierByFactory[factory] = Factory.NEUTRAL_YIELD_MULTIPLIER;
        }

        foreach (BabyDragonTower babyDragon in _babyDragons)
        {
            if (!babyDragon.CanOperate ||
                babyDragon.DragonData == null ||
                babyDragon.Mode != BabyDragonMode.Buff)
            {
                continue;
            }

            ApplyBuffFrom(babyDragon, _multiplierByFactory);
        }

        foreach (KeyValuePair<Factory, float> entry in _multiplierByFactory)
        {
            Debug.Log($"[BabyDragonBuffSystem] 최종 배율 → {entry.Key.name}: ×{entry.Value}");
            entry.Key.SetAreaYieldMultiplier(entry.Value);
        }

        BuffsRecomputed?.Invoke();
    }

    private void ApplyBuffFrom(BabyDragonTower babyDragon, Dictionary<Factory, float> multiplierByFactory)
    {
        float radius = babyDragon.DragonData.BuffRadius;

        // 0이면 버프 없는 속성(예: 얼음/불/시간 - 생산량 버프가 아닌 별개 지역 효과를 쓴다) - 계산을 건너뛴다.
        if (radius <= 0f)
        {
            return;
        }

        float radiusY = radius * IsometricMath.RADIUS_Y_RATIO;
        Vector3 center = babyDragon.transform.position;

        foreach (Factory factory in _factories)
        {
            if (!IsometricMath.IsWithinEllipse(factory.transform.position, center, radius, radiusY))
            {
                Debug.Log($"[BabyDragonBuffSystem] {factory.name}: 범위 밖 → 버프 없음");
                continue;
            }

            float kinBonus = _dragonTreeManager != null
                ? _dragonTreeManager.GetKinAreaYieldBonusRatio(babyDragon.DragonData.DragonType)
                : 0f;
            float babyDragonMultiplier = babyDragon.DragonData.BuffYieldMultiplier * (1f + kinBonus);

            float before = multiplierByFactory[factory];
            multiplierByFactory[factory] *= babyDragonMultiplier;
            Debug.Log($"[BabyDragonBuffSystem] {factory.name}: 범위 안 → ×{before} → ×{multiplierByFactory[factory]} (새끼용 배율 ×{babyDragonMultiplier})");
        }
    }
}
