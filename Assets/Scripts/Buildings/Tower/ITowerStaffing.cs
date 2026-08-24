/// <summary>
/// 타워의 가동 조건을 공급한다. TowerAttack이 GetComponent로 찾아 조회하며,
/// 구현체가 없으면 공격하지 않는다.
/// 일반 타워는 TowerPopulation(인구), 새끼용은 BabyDragonTower(슬라임 먹이)가 구현한다.
/// </summary>
public interface ITowerStaffing
{
    // TowerAuraSystem도 오라 활성 판정에 이 값을 그대로 쓴다(TowerPopulation의 CanOperate는
    // 오라로 받은 보너스 인구 SoulPopulation을 포함) - 그래서 오라와 이 값은 서로를 참조한다.
    // 그 순환은 TowerAuraSystem이 프레임마다 한 번 도는 고정점 반복으로 풀며, 계산 전에 프레임
    // 번호를 먼저 찍어 되짚어 들어온 조회가 표를 읽고 돌아가게 한다. 자세한 설명은 그 클래스 참고.
    bool CanOperate { get; }

    // 공격 발동 간격을 이 값으로 나눈다 - 1이 기준 속도. 오라 반경 스케일링에도 쓰인다.
    float StaffingRatio { get; }
}
