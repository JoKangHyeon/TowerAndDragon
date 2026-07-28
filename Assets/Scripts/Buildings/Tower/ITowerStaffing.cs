/// <summary>
/// 타워의 가동 조건을 공급한다. TowerAttack이 GetComponent로 찾아 조회하며,
/// 구현체가 없으면 공격하지 않는다.
/// 일반 타워는 TowerPopulation(인구), 새끼용은 BabyDragonTower(슬라임 먹이)가 구현한다.
/// </summary>
public interface ITowerStaffing
{
    bool CanOperate { get; }

    // 공격 발동 간격을 이 값으로 나눈다 - 1이 기준 속도.
    float StaffingRatio { get; }
}
