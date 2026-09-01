/// <summary>
/// 누적 일차(1부터 세는 통산 날짜)를 화면에 보이는 "몇 주기 몇 일차"로 옮기는 규칙.
/// 인게임 창의 날짜 표기와 게임오버 창의 최종 일차가 같은 계산·같은 서식을 쓰도록 한곳에 모았다
/// (CLAUDE.md 커밋규칙 §3.2 - 스크립트 2개 이상이 공유하는 문자열).
///
/// 주기는 0부터, 주기 안의 일차는 1부터 센다. 즉 1~7일차가 0주기 1~7일차이고 8일차가 1주기 1일차다.
/// </summary>
public static class CycleCalendar
{
    /// <summary>
    /// 한 주기의 길이(일). 마지막 날 밤이 보스 웨이브이고, 그 다음 날부터 새 주기가 시작된다.
    /// 인게임 창의 웨이브 진행 바 눈금 수도 이 값에서 끌어 쓴다.
    /// </summary>
    public const int DAYS_PER_CYCLE = 7;

    // 날짜 표기 형식은 스트링테이블에서 가져온다(언어별 문구·자리표시자 위치가 다름).
    // {0} = 주기(0부터), {1} = 그 주기 안에서의 일차(1..DAYS_PER_CYCLE).
    // 값 예) en_us: "CYCLE {0} DAY {1}" / ko_kr: "{0}주기 {1}일차"
    private const string DAY_LOC_KEY = "main_day";

    private static string DayFormat => StringTable.GetString(DAY_LOC_KEY);

    /// <summary>누적 일차가 몇 번째 주기(0부터)에 속하는지.</summary>
    public static int CycleIndexOf(int dayNumber) => ElapsedDays(dayNumber) / DAYS_PER_CYCLE;

    /// <summary>누적 일차가 그 주기 안에서 몇 일차(1부터)인지.</summary>
    public static int DayInCycleOf(int dayNumber) => ElapsedDays(dayNumber) % DAYS_PER_CYCLE + 1;

    /// <summary>누적 일차를 현재 언어의 "주기/일차" 문구로 만든다.</summary>
    public static string FormatDayLabel(int dayNumber) =>
        string.Format(DayFormat, CycleIndexOf(dayNumber), DayInCycleOf(dayNumber));

    // dayNumber는 1부터 세는 누적 일차다. 주기로 나누려면 0부터 세는 경과 일수로 내려야 한다.
    // 아직 첫 낮이 오지 않아 0 이하인 동안에는 첫날과 같게 본다(0주기 1일차).
    private static int ElapsedDays(int dayNumber) => dayNumber > 0 ? dayNumber - 1 : 0;
}
