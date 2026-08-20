using UnityEngine;

/// <summary>
/// 인구 할당 없이 항상 100% 효율로 가동되는 타워를 위한 Staffing 컴포넌트.
/// 영혼 타워처럼 자체 인구가 필요 없는 특수 타워 프리팹에 TowerPopulation 대신 부착합니다.
/// </summary>
public class AlwaysOnStaffing : MonoBehaviour, ITowerStaffing
{
    public bool CanOperate => true;
    public float StaffingRatio => 1f;
}
