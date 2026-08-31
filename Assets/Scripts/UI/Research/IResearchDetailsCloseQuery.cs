/// <summary>
/// 연구 창 안쪽의 빈 곳을 눌러 상세 패널을 닫아도 되는지 묻는다.
///
/// 본게임은 닫아 준다 - 상세 패널에는 닫기 버튼이 없고 트리의 오른쪽을 덮으므로, 그 아래 노드를
/// 고르려면 빈 곳을 눌러 패널을 물릴 방법이 필요하다.
/// 튜토리얼의 노드 해금 단계만 막는다 - 그 단계의 딤 구멍은 트리와 상세 패널을 함께 덮어야 하는데
/// 구멍이 사각형 하나라 <b>둘 사이의 공백까지 함께 뚫린다</b>. 안내를 따라 누른 클릭이 그 공백에
/// 떨어지면 방금 연 패널이 닫혀, 눌러야 할 해금 버튼이 사라진다.
///
/// UI_ResearchWindow가 이 질문을 받을 대상을 모르도록 인터페이스로 끊어 둔다
/// (<see cref="IUnreachableChunkSelectQuery"/>와 같은 주입 방식).
/// </summary>
public interface IResearchDetailsCloseQuery
{
    bool CanCloseResearchDetails();
}
