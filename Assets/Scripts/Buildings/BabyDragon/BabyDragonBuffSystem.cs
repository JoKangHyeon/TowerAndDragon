using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 새끼용 주변 생산건물(Factory)에 생산량 배율 버프를 적용한다.
/// 버프 반경은 BabyDragonData.BuffRadius(공격 사거리와 무관한 독립 값)를 쓴다 - Attack을
/// 아예 안 붙인 버프 전용 개체(예: 생명 속성)도 버프를 낼 수 있어야 하기 때문이다.
/// TowerAttack과 동일한 IsometricMath 타원 판정을 좌표만으로 수행하므로 생산건물에
/// 콜라이더가 없어도 동작한다.
/// 이동은 OnBuildingAdded/OnBuildingRemoving을 발행하지 않으므로(GridMap.MoveBuilding),
/// 등록된 인스턴스의 현재 transform.position을 매번 새로 읽어 재계산한다.
/// </summary>
public class BabyDragonBuffSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private CycleManager _cycleManager;

    private readonly List<BabyDragonTower> _babyDragons = new();
    private readonly List<Factory> _factories = new();

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
        }

        if (_cycleManager != null)
        {
            // Factory.OnSettlement가 OnDayStart에서 실행되므로(Factory.cs:36), 그 전에
            // 버프를 확정해야 한다. OnNightEnd -> StartDay -> OnDayStart 순서가 같은 호출
            // 스택 안에서 보장되므로(CycleManager.cs:52-56) OnNightEnd에서 재계산한다.
            _cycleManager.OnNightEnd.AddListener(RecomputeAll);
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightEnd.RemoveListener(RecomputeAll);
        }
    }

    private void HandleBuildingAdded(Building building)
    {
        if (building is BabyDragonTower babyDragon && !_babyDragons.Contains(babyDragon))
        {
            _babyDragons.Add(babyDragon);
        }
        else if (building is Factory factory && !_factories.Contains(factory))
        {
            // 1일차(OnNightEnd가 아직 한 번도 안 불린 시점)에 배치돼도 무버프 상태로 정직하게 시작한다.
            factory.SetAreaYieldMultiplier(Factory.NEUTRAL_YIELD_MULTIPLIER);
            _factories.Add(factory);
        }
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (building is BabyDragonTower babyDragon)
        {
            _babyDragons.Remove(babyDragon);
        }
        else if (building is Factory factory)
        {
            _factories.Remove(factory);
        }
    }

    // CanOperate(먹이 상태)는 그날 아침 BabyDragonFeedingSystem.OnDayStart가 갱신한 값이다.
    // 즉 이 밤 시점의 버프는 "그날 아침 먹었는지"를 반영하고, 다음 날 아침 정산에 쓰인다 -
    // 하루 지연이지만 순환 의존은 아니다(먹이 계산에는 버프가 관여하지 않음).
    private void RecomputeAll(int _)
    {
        var multiplierByFactory = new Dictionary<Factory, float>();

        foreach (Factory factory in _factories)
        {
            multiplierByFactory[factory] = Factory.NEUTRAL_YIELD_MULTIPLIER;
        }

        foreach (BabyDragonTower babyDragon in _babyDragons)
        {
            if (!babyDragon.CanOperate || babyDragon.DragonData == null)
            {
                continue;
            }

            ApplyBuffFrom(babyDragon, multiplierByFactory);
        }

        foreach (KeyValuePair<Factory, float> entry in multiplierByFactory)
        {
            entry.Key.SetAreaYieldMultiplier(entry.Value);
        }
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
                continue;
            }

            multiplierByFactory[factory] *= babyDragon.DragonData.BuffYieldMultiplier;
            Debug.Log($"[BabyDragonBuffSystem] {multiplierByFactory[factory]}에 {babyDragon.DragonData.BuffYieldMultiplier}만큼 버프 적용");
        }
    }
}
