/// <summary>
/// 밤으로 넘어가기 직전의 확인창을 지금 띄워도 되는지 묻는다.
/// <see cref="IDayEndBlockQuery"/>로 등록된 대상 중 이것도 구현한 쪽에게만 묻는다 -
/// "밤을 막을 것인가"와 "확인창을 띄울 것인가"는 답이 다르다.
///
/// 안내(<see cref="UI_GuideOverlay"/>)가 딤으로 화면을 막고 있는 동안이 그 경우다.
/// 확인창은 HUD 안에 있어 딤보다 <b>아래</b>로 그려지므로 YES/NO를 누를 수 없고,
/// 확인창의 전체 화면 가림막이 딤에 뚫어 둔 구멍(밤 버튼)까지 덮어 밤 버튼도 함께 죽는다.
/// 그러면 낮이 영영 끝나지 않아 안내도 다음 단계로 가지 못한다.
/// </summary>
public interface IDayEndConfirmBlockQuery
{
    bool CanShowDayEndConfirm();
}
