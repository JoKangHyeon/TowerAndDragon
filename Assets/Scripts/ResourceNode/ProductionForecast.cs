using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 현재 건설된 생산시설(Factory)들의 '다음 정산 예상 생산량'을 자원 종류별로 집계한다.
/// 실제 정산은 각 Factory가 OnDayStart에 수행하며, 이 클래스는 같은 공식(Factory.AccumulateProjectedProduction)으로
/// 예상치만 계산해 UI에 제공한다. 건물 추가/제거·인구 배치 변경 시 다시 계산하고 ForecastChanged로 알린다.
/// </summary>
public class ProductionForecast : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;

    [Tooltip("인구 배치(충원율) 변경 시 예상치를 다시 계산하기 위해 구독한다.")]
    [SerializeField] private PopulationManager _populationManager;

    [Tooltip("새끼용 지역 생산 버프가 재확정될 때 예상치를 다시 계산하기 위해 구독한다. " +
        "미연결이면 이 값 변동만으로는 예상치가 갱신되지 않는다(다음 건물 추가/제거·인구 변경 때 함께 반영됨).")]
    [SerializeField] private BabyDragonBuffSystem _babyDragonBuffSystem;

    /// <summary>예상 생산량이 바뀌었을 때 발화. UI가 구독해 표기를 갱신한다.</summary>
    public UnityEvent ForecastChanged;

    private readonly Dictionary<ResourceType, int> _projected = new();

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingChanged);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingChanged);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        if (_babyDragonBuffSystem != null)
        {
            _babyDragonBuffSystem.BuffsRecomputed.AddListener(HandleBuffsRecomputed);
        }

        Recompute();
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingChanged);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingChanged);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_babyDragonBuffSystem != null)
        {
            _babyDragonBuffSystem.BuffsRecomputed.RemoveListener(HandleBuffsRecomputed);
        }
    }

    // 자원 종류별 예상 하루 생산량. 없으면 0.
    public int GetDailyProduction(ResourceType type) =>
        _projected.TryGetValue(type, out int amount) ? amount : 0;

    private void HandleBuildingChanged(Building building) => Recompute();
    private void HandlePopulationChanged(PopulationState state) => Recompute();
    private void HandleBuffsRecomputed() => Recompute();

    private void Recompute()
    {
        _projected.Clear();

        if (_gridMap != null)
        {
            foreach (Building building in _gridMap.Buildings)
            {
                if (building is Factory factory)
                {
                    factory.AccumulateProjectedProduction(_projected);
                }
            }
        }

        ForecastChanged?.Invoke();
    }
}
