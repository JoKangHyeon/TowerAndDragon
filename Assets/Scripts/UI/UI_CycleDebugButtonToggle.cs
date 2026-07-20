using UnityEngine;

// CycleManager.OnDayStart/OnNightStart가 직접 호출 - 낮/밤 디버그 버튼(NightButton/DayButton) 중
// 현재 상태에 맞는 쪽만 활성화한다.
public class UI_CycleDebugButtonToggle : MonoBehaviour
{
    [SerializeField]
    private GameObject _nightButton;

    [SerializeField]
    private GameObject _dayButton;

    public void ShowNightButton()
    {
        _nightButton.SetActive(true);
        _dayButton.SetActive(false);
    }

    public void ShowDayButton()
    {
        _nightButton.SetActive(false);
        _dayButton.SetActive(true);
    }
}
