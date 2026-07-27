// 게이트 평가(ProgressionGateSO.IsSatisfied)에 필요한 참조 묶음.
// 파생 게이트가 필요한 값을 GameManager.CurrentRun 등에서 직접 꺼내 쓴다.
public sealed class ProgressionContext
{
    public GameManager Game { get; }
    public CycleManager Cycle { get; }
    public IProgressionState State { get; }

    public ProgressionContext(GameManager game, CycleManager cycle, IProgressionState state)
    {
        Game = game;
        Cycle = cycle;
        State = state;
    }
}
