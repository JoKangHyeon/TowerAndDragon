/// <summary>
/// 모드 자신만 아는 진입 조건(밤에는 켤 수 없다 등)을 <see cref="UIManager.OpenExclusive"/>에 알린다.
///
/// 버튼은 각 창의 진입점(ToggleFromEntryPoint·ToggleConquestMode 등)을 지나므로 그 안에 둔 가드로
/// 충분하지만, 단축키는 UIManager가 <see cref="IExclusiveMode.Open"/>을 직접 부르기 때문에 진입점을
/// 지나지 않는다 - 가드를 진입점에만 두면 <b>마우스로는 막힌 모드가 키로는 열린다</b>.
/// 그래서 판정을 모드 쪽에 두고, 어느 경로든 반드시 지나는 OpenExclusive가 한 번 물어본다.
///
/// 거절 이유를 알리는 것(경고창 등)도 구현체가 맡는다. 그러지 않으면 버튼은 경고가 뜨고 단축키는
/// 조용히 무시되는 식으로 두 경로가 다시 갈린다.
/// 구현하지 않은 모드는 늘 열린다(기존 동작 유지) - 다른 관문과 같은 관례다.
/// </summary>
public interface IExclusiveModeEntryGuard
{
    /// <summary>지금 이 모드에 들어가도 되는지. 거절할 때 그 이유를 플레이어에게 알린다.</summary>
    bool CanEnterNow();
}
