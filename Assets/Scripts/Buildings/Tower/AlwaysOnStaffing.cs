using UnityEngine;

/// <summary>
/// 인구 할당 없이 항상 100% 효율로 가동되는 타워를 위한 Staffing 컴포넌트.
/// 영혼 타워처럼 자체 인구가 필요 없는 특수 타워 프리팹에 TowerPopulation 대신 부착합니다.
///
/// IPopulationAllocationTarget도 함께 구현한다 - Building_window(UI_PopulationAllocationWindow)는
/// building.GetComponent&lt;IPopulationAllocationTarget&gt;()로만 정보창 대상을 찾는데, 이걸 구현하는
/// 컴포넌트가 없으면 다른 타워와 달리 클릭해도 정보창이 조용히 안 열린다. 여기서는 항상 "정원만큼
/// 가득 찬" 읽기 전용 값으로 노출해 배치/회수 버튼은 자연히 비활성/무동작하게 만든다.
/// </summary>
[RequireComponent(typeof(Tower))]
public class AlwaysOnStaffing : MonoBehaviour, ITowerStaffing, IPopulationAllocationTarget
{
    private Tower _tower;

    public bool CanOperate => true;
    public float StaffingRatio => 1f;

    public int Capacity => _tower != null ? _tower.PopulationCapacity : 0;
    public int AssignedPopulation => Capacity;
    public int AvailableCapacity => 0;
    public bool IsInitialized => _tower != null && _tower.Data != null;

    private void Awake()
    {
        _tower = GetComponent<Tower>();
    }

    // 항상 가득 찬 것으로 취급하므로 실제 배치/회수는 없다 - 호출은 안전하게 무시한다.
    public bool TryAssign(int amount) => false;
    public bool TryUnassign(int amount) => false;
}
