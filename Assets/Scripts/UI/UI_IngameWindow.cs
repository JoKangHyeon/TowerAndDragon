using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 창 컨트롤러. 이 창에 부착해, 창 안의 UI 요소를 한곳에서 관리한다.
/// 현재는 낮/밤 심볼(Symbol_Day) 전환을 담당 — 낮이면 Image_Day, 밤이면 Image_Light를 켠다.
/// CycleManager.OnCycleChanged를 구독해 상태 변화에 반응하고, 활성화 시점의 현재 상태도 즉시 반영한다.
/// </summary>
public class UI_IngameWindow : MonoBehaviour
{
    [SerializeField] private CycleManager _cycleManager;

    [Header("낮/밤 심볼")]
    [Tooltip("낮에 켜질 아이콘.")]
    [SerializeField] private GameObject _imageDay;
    [Tooltip("밤에 켜질 아이콘.")]
    [SerializeField] private GameObject _imageLight;

    [Header("낮/밤 하단 컨트롤")]
    [Tooltip("낮에 켜질 버튼(다음 밤으로 진행).")]
    [SerializeField] private GameObject _buttonNextNight;
    [Tooltip("밤에 켜질 속도 조절 UI.")]
    [SerializeField] private GameObject _speedSetting;

    private void Awake()
    {
        // '다음 밤으로' 버튼: 누르면 낮을 종료하고 밤을 시작한다.
        if (_buttonNextNight != null)
        {
            Button button = _buttonNextNight.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(GoToNight);
            }
        }
    }

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(ApplyCycle);
            ApplyCycle(_cycleManager.CurrentCycle);
        }
    }

    private void GoToNight()
    {
        if (_cycleManager != null)
        {
            _cycleManager.EndDay();
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(ApplyCycle);
        }
    }

    private void ApplyCycle(CycleManager.CycleState state)
    {
        bool isDay = state == CycleManager.CycleState.Day;

        if (_imageDay != null)
        {
            _imageDay.SetActive(isDay);
        }

        if (_imageLight != null)
        {
            _imageLight.SetActive(!isDay);
        }

        // 낮에는 '다음 밤으로' 버튼, 밤에는 속도 조절 UI를 켠다.
        if (_buttonNextNight != null)
        {
            _buttonNextNight.SetActive(isDay);
        }

        if (_speedSetting != null)
        {
            _speedSetting.SetActive(!isDay);
        }
    }
}
