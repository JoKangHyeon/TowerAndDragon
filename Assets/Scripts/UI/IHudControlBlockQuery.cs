/// <summary>
/// 배타 창도 밤 시작도 아닌 HUD 조작(게임 속도 등)을 막을지 묻는다.
///
/// 안내가 도는 동안에는 지금 유도하는 것 외의 버튼이 동작하면 안 된다. 배타 창은
/// <see cref="IExclusiveModeOpenQuery"/>가, 밤 시작은 <see cref="IDayEndBlockQuery"/>가 막지만
/// 그 관문을 지나지 않는 조작이 남는다 - 이 인터페이스가 그 나머지를 맡는다.
///
/// 배선되지 않은 씬에서는 참조가 null로 남아 기존 동작이 그대로다(다른 두 관문과 같은 관례).
/// </summary>
public interface IHudControlBlockQuery
{
    /// <summary>지금 이 조작을 허용해도 되는가. false면 호출부는 조용히 무시한다.</summary>
    bool CanUseHudControl();
}
