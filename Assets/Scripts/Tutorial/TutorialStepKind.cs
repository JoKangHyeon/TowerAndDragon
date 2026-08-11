/// <summary>
/// 단계가 어떻게 끝나는지. 설명형(Acknowledge)을 행동형과 같이 두면 플레이어가 뭘 해야 할지 모른 채 멈춘다.
/// </summary>
public enum TutorialStepKind
{
    // 완료 조건이 충족될 때까지 안내를 유지한다.
    WaitForAction = 0,

    // 읽히기만 하면 되는 설명. 확인 버튼을 누르거나 정해진 시간이 지나면 넘어간다.
    Acknowledge,
}
