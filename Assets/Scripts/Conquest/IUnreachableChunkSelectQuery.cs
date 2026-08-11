/// <summary>
/// 아직 갈 수 없는 땅(내 땅과 떨어져 있어 원정을 보낼 수 없는 곳)을 골랐을 때
/// 점령 패널을 열어도 되는지 묻는다.
///
/// 본게임은 열어 준다 - 규칙을 아는 플레이어는 패널에서 비용과 이유를 확인하고 스스로 닫는다.
/// 튜토리얼만 막는다 - 처음 하는 플레이어는 점령 버튼이 왜 안 듣는지 모른 채 갇힌다.
/// ConquestModeController가 이 질문을 받을 대상을 모르도록 인터페이스로 끊어 둔다
/// (CycleManager.DayEndBlockQuery와 같은 주입 방식).
/// </summary>
public interface IUnreachableChunkSelectQuery
{
    bool CanSelectUnreachableChunk();
}
