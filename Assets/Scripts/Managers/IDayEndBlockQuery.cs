/// <summary>
/// 지금 낮을 끝내도 되는지 묻는다. 안내가 도는 동안 플레이어가 밤으로 넘어가 버리는 것을 막는 용도다.
/// 배선되지 않은 씬에서는 CycleManager가 null로 두고 언제나 허용한다(기존 동작 유지).
/// </summary>
public interface IDayEndBlockQuery
{
    bool CanEndDay();
}
