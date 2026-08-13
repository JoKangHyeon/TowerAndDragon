using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 아침 정산 중 유지비(소비) 단계를 담당한다. CycleManager.OnDayStartUpkeep은
/// OnDayStart(생산 정산) 다음에 발화하므로, 당일 생산분이 반영된 재고를 기준으로 소비한다.
/// 각 단계의 세부 계산과 상태 변경은 전용 시스템에 위임한다.
/// </summary>
public class DailySettlementManager : MonoBehaviour
{
    private const int FIRST_SETTLEMENT_DAY = 2;

    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private PopulationUpkeepSystem _populationUpkeepSystem;

    [Tooltip("설원·암석 지역 시설의 자재 유지비. 미연결이면 지역 유지비를 소비하지 않는다.")]
    [SerializeField] private TerrainUpkeepSystem _terrainUpkeepSystem;

    [Tooltip("가동 중인 타워의 자재 유지비. 미연결이면 타워 유지비를 소비하지 않는다.")]
    [SerializeField] private TowerUpkeepSystem _towerUpkeepSystem;

    public UnityEvent<int, PopulationUpkeepResult> SettlementCompleted;

    // 씬 YAML에 이 필드 항목이 없는 기존 인스턴스에서도 null이 되지 않도록 인라인 초기화한다
    // (CycleManager.OnDayStartUpkeep과 같은 이유).
    public UnityEvent<int, TerrainUpkeepResult> TerrainSettlementCompleted = new();

    // 위와 같은 이유로 인라인 초기화한다.
    public UnityEvent<int, TowerUpkeepResult> TowerSettlementCompleted = new();

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStartUpkeep.AddListener(Settle);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStartUpkeep.RemoveListener(Settle);
        }
    }

    private void Settle(int currentDay)
    {
        // 1일차 시작에는 정산할 전날이 없으므로 유지비를 소비하지 않는다.
        if (currentDay < FIRST_SETTLEMENT_DAY)
        {
            return;
        }

        SettleFood(currentDay);
        SettleTerrain(currentDay);
        SettleTower(currentDay);
    }

    private void SettleFood(int currentDay)
    {
        if (_populationUpkeepSystem == null ||
            !_populationUpkeepSystem.TrySettle(out PopulationUpkeepResult result))
        {
            return;
        }

        SettlementCompleted?.Invoke(currentDay, result);
    }

    // 식량 유지비 다음에 실행한다 - 기아로 인구가 줄면 그만큼 자재 유지비도 줄어드는 게
    // 자연스럽고, 반대 순서면 이미 굶어 죽을 인구 몫의 자재까지 먼저 걷게 된다.
    private void SettleTerrain(int currentDay)
    {
        if (_terrainUpkeepSystem == null ||
            !_terrainUpkeepSystem.TrySettle(out TerrainUpkeepResult result))
        {
            return;
        }

        TerrainSettlementCompleted?.Invoke(currentDay, result);
    }

    // 지역 유지비 다음에 실행한다 - 앞 단계에서 인구가 줄면(기아·시설 비활성화) 그만큼 타워
    // 유지비도 줄어드는 게 자연스럽고, 반대 순서면 이미 사라질 인구 몫까지 먼저 걷게 된다
    // (식량 → 지역 순서를 정한 것과 같은 이유).
    private void SettleTower(int currentDay)
    {
        if (_towerUpkeepSystem == null ||
            !_towerUpkeepSystem.TrySettle(out TowerUpkeepResult result))
        {
            return;
        }

        TowerSettlementCompleted?.Invoke(currentDay, result);
    }
}
