/// <summary>뮤테이터 소비 지점이 매번 null 검사를 반복하지 않게 하는 정적 헬퍼.
/// 서비스 참조는 어디서나 <c>[WiringOptional]</c>이므로(튜토리얼·테스트 씬에는 없다)
/// 소비 지점마다 "null이면 중립" 분기를 쓰게 되는데, 그 분기가 한 곳이라도 빠지면 NRE가 된다.</summary>
public static class RunModifiers
{
    public static RunModifierSnapshot SnapshotOf(RunModifierService service)
    {
        return service != null ? service.ActiveSnapshot : RunModifierSnapshot.Neutral;
    }
}
