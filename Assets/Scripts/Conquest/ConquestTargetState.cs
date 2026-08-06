// 점령 모드에서 표시 중인 청크가 지금 어떤 상태인지 - 선 색과 면 표시 여부를 결정한다.
public enum ConquestTargetState
{
    Conquerable, // 지금 원정을 보낼 수 있다
    InProgress, // 원정을 보내 진행 중이다
    Unreachable // 인접 점령지가 없어 아직 보낼 수 없다
}
