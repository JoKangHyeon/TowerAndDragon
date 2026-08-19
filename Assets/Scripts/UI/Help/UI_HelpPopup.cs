using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 어떤 요소를 처음 만났을 때 1회 뜨는 설명 모달. 열려 있는 동안 게임이 멈추고,
/// 확인을 눌러야 닫힌다 - dim 클릭으로 닫히는 길을 두지 않는다(읽어야 하는 팝업이다).
///
/// <b>IExclusiveMode를 구현하되 UIManager._exclusiveModeBehaviours에는 등록하지 않는다.</b>
/// 등록하면 CloseAllExcept의 대상이 되어, 시스템이 띄운 이 팝업이 플레이어가 보던 건설·연구 창을
/// 강제로 닫는다(건설 창을 연 채 건물을 짓는 것이 정상 흐름이다). 그렇다고 구현조차 안 하면
/// UIManager가 캔버스 자식에서 IExclusiveMode를 긁어 모으는 Esc 목록에 안 잡혀,
/// Esc로 이 팝업을 닫는 프레임에 설정 창이 대신 열린다.
/// 여는 것도 OpenExclusive가 아니라 HelpDiscoveryController가 <see cref="Show"/>를 직접 부른다.
/// </summary>
public sealed class UI_HelpPopup : MonoBehaviour, IExclusiveMode
{
    [Tooltip("게임 정지와 Esc 조정에 쓴다.")]
    [SerializeField] private UIManager _uiManager;

    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _bodyText;

    [Tooltip("항목의 그림을 띄울 자리. 그림이 없는 항목에서는 오브젝트째 꺼져 패널 높이가 줄어든다.")]
    [SerializeField] private Image _illustrationImage;

    [SerializeField] private Button _confirmButton;

    [Tooltip("Esc로도 닫을 수 있게 한다. 확인 버튼과 같은 경로로 닫힌다.")]
    [SerializeField] private InputActionReference _closeAction;

    /// <summary>
    /// 닫혔다. 대기 중인 다음 팝업을 여는 신호다 - 큐는 이 컴포넌트가 아니라
    /// HelpDiscoveryController가 들고 있다.
    /// 인라인 초기화가 필요하다 - 기존 씬·프리팹 YAML에 이 필드가 없어 null로 남는다.
    /// </summary>
    public UnityEvent Closed = new();

    private bool _isOpen;

    private GameSpeedManager GameSpeed => _uiManager != null ? _uiManager.GameSpeed : null;

    /// <summary>떠 있는가. 큐가 다음 항목을 겹쳐 띄우지 않도록 본다.</summary>
    public bool IsOpen => _isOpen;

    bool IExclusiveMode.IsOpen => IsOpen;

    // UIManager가 직접 열 일은 없다(배열에 등록하지 않는다). 내용 없이 열리지 않도록
    // 인터페이스 경로로는 아무것도 하지 않는다 - 여는 길은 Show 하나뿐이다.
    void IExclusiveMode.Open() { }

    void IExclusiveMode.Close() => Close();

    /// <summary>이 항목의 설명을 띄운다. 이미 떠 있으면 내용을 갈아 끼운다.</summary>
    public void Show(HelpEntrySO entry)
    {
        if (entry == null)
        {
            return;
        }

        // SetActive 이전에 세운다 - 비활성으로 저장된 오브젝트의 Awake는 첫 SetActive(true) 안에서
        // 동기 실행되므로, 이 플래그가 없으면 Awake의 초기 닫기가 방금 연 팝업을 도로 닫는다
        // (CLAUDE.md 이벤트 초기화 규칙).
        _isOpen = true;
        gameObject.SetActive(true);

        Render(entry);
        SoundManager.Play(SoundId.UiWindowOpen);
    }

    public void Close()
    {
        // 열린 적이 없는데 들어온 호출(Awake의 초기 닫기 등)은 Closed를 발화시키지 않는다 -
        // 큐가 자기 차례를 잃는다.
        if (!_isOpen)
        {
            gameObject.SetActive(false);
            return;
        }

        SoundManager.Play(SoundId.UiWindowClose);

        _isOpen = false;
        gameObject.SetActive(false);
        Closed.Invoke();
    }

    private void Render(HelpEntrySO entry)
    {
        if (_titleText != null)
        {
            _titleText.text = StringTable.GetString(entry.TitleLocKey);
        }

        if (_bodyText != null)
        {
            _bodyText.text = StringTable.GetString(entry.BodyLocKey);
        }

        // 그림이 없는 항목에서 빈 액자가 남지 않도록 오브젝트째 끈다
        // (UI_TutorialEndingPanel의 일러스트 처리와 같다).
        if (_illustrationImage != null)
        {
            _illustrationImage.sprite = entry.Illustration;
            _illustrationImage.gameObject.SetActive(entry.Illustration != null);
        }
    }

    private void Awake()
    {
        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(Close);
        }

        // 인스턴스가 활성으로 저장돼 있어도 시작 시 닫힌 상태를 보장하되,
        // 방금 Show()가 유발한 Awake라면 닫지 않는다.
        if (!_isOpen)
        {
            gameObject.SetActive(false);
        }
    }

    // 정지는 짝이 보장되는 OnEnable/OnDisable에 건다(UI_ConfigWindow와 같은 이유).
    private void OnEnable()
    {
        GameSpeedManager gameSpeed = GameSpeed;
        if (gameSpeed != null)
        {
            gameSpeed.AddWindowPause();
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed += OnCloseActionPerformed;
        }
    }

    private void OnDisable()
    {
        GameSpeedManager gameSpeed = GameSpeed;
        if (gameSpeed != null)
        {
            gameSpeed.RemoveWindowPause();
        }

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }
    }

    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        Close();
    }
}
