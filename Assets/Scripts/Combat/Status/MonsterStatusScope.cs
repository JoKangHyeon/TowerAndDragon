/// <summary>
/// 상태 줄이 "누구에게" 걸린 것인지. 툴팁이 구역을 나누는 기준이며,
/// 하드모드 전역 버프처럼 새 출처가 생겨도 이 둘 중 하나로 분류된다.
/// </summary>
public enum MonsterStatusScope
{
    /// <summary>지금 커서를 올린 그 개체에만 걸린 것 (새끼용 화상, 타워 둔화 등).</summary>
    Instance,

    /// <summary>이번 밤 모든 적에게 걸린 것 (점령 기반 강화, 하드모드 전역 버프).</summary>
    Global,
}
