/// <summary>
/// 인구가 배치되는 사용처의 상위 분류를 정의한다.
/// 구체적인 건물 종류가 아니라 타워, 생산, 점령의 시스템 책임을 구분한다.
/// </summary>
public enum PopulationAssignmentType
{
    None,
    Tower,
    Production,
    Conquest
}
