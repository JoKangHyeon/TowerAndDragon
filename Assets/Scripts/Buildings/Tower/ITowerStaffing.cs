/// <summary>
/// 타워의 가동 조건을 공급한다. TowerAttack이 GetComponent로 찾아 조회하며,
/// 구현체가 없으면 공격하지 않는다.
/// 일반 타워는 TowerPopulation(인구), 새끼용은 BabyDragonTower(슬라임 먹이)가 구현한다.
/// </summary>
public interface ITowerStaffing
{
    // TowerAuraSystem도 오라 활성 판정에 이 값을 그대로 쓴다(TowerPopulation의 CanOperate는
    // 오라로 받은 보너스 인구 SoulPopulation을 포함) - 그 상호 참조가 만드는 재귀는
    // TowerAuraSystem.ResolveModifiers의 재진입 가드가 끊는다. 가드 설명은 그 클래스 참고.
    bool CanOperate { get; }

    // 공격 발동 간격을 이 값으로 나눈다 - 1이 기준 속도. 오라 반경 스케일링에도 쓰인다.
    float StaffingRatio { get; }
}
