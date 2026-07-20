using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 창 컨트롤러. 이 창에 부착해, 창 안의 UI 요소를 한곳에서 관리한다.
/// - 낮/밤 심볼(Symbol_Day) 전환과 하단 컨트롤 표시 (CycleManager.OnCycleChanged 구독)
/// - Panel_TopLeft 자원 보유량 표시 (ResourceManager 이벤트 구독)
/// 활성화 시점의 현재 상태도 즉시 반영한다. (인구 표시는 별도 인구 시스템에서 연결 예정)
/// </summary>
public class UI_IngameWindow : MonoBehaviour
{
    // 자원 표시 1칸: 자원 종류 ↔ 수량 텍스트.
    [System.Serializable]
    private struct ResourceSlot
    {
        public ResourceType Type;
        public TMP_Text AmountText;
    }

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

    [Header("자원 표시 (Panel_TopLeft)")]
    [SerializeField] private ResourceManager _resourceManager;
    [Tooltip("자원 종류별 수량 텍스트. 기본 3종 + 특화 4종.")]
    [SerializeField] private ResourceSlot[] _resourceSlots;

    [Header("점령 (Panel_BottomRight)")]
    [Tooltip("점령 모드 토글 버튼.")]
    [SerializeField] private Button _buttonConquest;
    [Tooltip("점령 정보 창. 버튼 클릭 시 점령 모드를 토글한다.")]
    [SerializeField] private UI_ConquestWindow _conquestWindow;

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

        // 점령 버튼: 누를 때마다 점령 모드를 켜고 끈다(토글). 실제 모드 처리는 점령 창이 담당.
        if (_buttonConquest != null && _conquestWindow != null)
        {
            _buttonConquest.onClick.AddListener(_conquestWindow.ToggleConquestMode);
        }
    }

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(ApplyCycle);
            ApplyCycle(_cycleManager.CurrentCycle);
        }

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged += RenderResource;
            RenderAllResources();
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

        if (_resourceManager != null)
        {
            _resourceManager.ResourceChanged -= RenderResource;
        }
    }

    // 활성화 시점의 보유량을 전 슬롯에 즉시 반영한다(이벤트를 놓친 초기 지급분 포함).
    private void RenderAllResources()
    {
        foreach (ResourceSlot slot in _resourceSlots)
        {
            RenderResource(slot.Type, _resourceManager.GetAmount(slot.Type));
        }
    }

    private void RenderResource(ResourceType type, int amount)
    {
        foreach (ResourceSlot slot in _resourceSlots)
        {
            if (slot.Type == type && slot.AmountText != null)
            {
                slot.AmountText.text = amount.ToString();
            }
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
