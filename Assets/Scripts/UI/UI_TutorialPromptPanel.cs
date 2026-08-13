using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 새 게임을 누른 뒤 "튜토리얼을 진행하시겠습니까?"를 묻는 창(StartScene의 TutorialBridge_window/Panel).
/// 진행이면 튜토리얼 씬, 건너뛰기면 본게임 씬으로 간다.
///
/// 어느 쪽으로 가도 정적 상태를 세팅하지 않는 것이 계약이다 - <see cref="SaveLoadRequest"/>가 비어 있어야
/// 게임 씬의 GameManager가 새 런으로 시작한다(<see cref="SceneNames"/>, TutorialToGameHandoff와 같은 판단).
/// 비우는 일은 이 창을 여는 UI_TitleWindow.StartNewGame이 이미 한다.
///
/// 묻는 것은 새 게임뿐이다. 이어하기·불러오기는 이 창을 거치지 않으므로 "튜토리얼을 봤는가" 같은
/// 저장 상태가 필요 없다.
///
/// 창 루트(전면 dim Image)에 부착해 바깥 클릭 닫기까지 겸한다 - UI_LoadGameWindow와 같은 구조.
/// </summary>
public sealed class UI_TutorialPromptPanel : MonoBehaviour
{
    [Tooltip("튜토리얼을 진행한다. 튜토리얼 씬으로 넘어간다.")]
    [SerializeField] private Button _proceedButton;

    [Tooltip("튜토리얼을 건너뛴다. 본게임 씬으로 넘어간다.")]
    [SerializeField] private Button _skipButton;

    [Tooltip("창 바깥(전면 dim) 클릭으로 닫기. 보통 이 창 루트의 Button.")]
    [SerializeField] private Button _blockerButton;

    [Tooltip("창을 닫는 키 - 보통 Esc.")]
    [SerializeField] private InputActionReference _closeAction;

    [Header("라벨")]
    [SerializeField] private LocalizedText _messageLabel;
    [SerializeField] private LocalizedText _proceedLabel;
    [SerializeField] private LocalizedText _skipLabel;

    // 건너뛰기로 열 씬. UI_TitleWindow가 Construct로 넣어 준다(UI_LoadGameWindow와 같은 주입 방식) -
    // 씬 이름의 소유자를 한 곳으로 남겨 두기 위해서다.
    private string _gameSceneName;

    // 씬 로드는 되돌릴 수 없으므로 두 번 눌려도 한 번만 나간다(UI_TutorialQuitPanel과 같은 방어).
    private bool _hasRequested;

    private bool _isOpen;

    private void Awake()
    {
        if (_proceedButton != null)
        {
            _proceedButton.onClick.AddListener(ProceedToTutorial);
        }

        if (_skipButton != null)
        {
            _skipButton.onClick.AddListener(SkipToGame);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.AddListener(Close);
        }

        ApplyLabelKeys();

        // 인스턴스가 활성으로 저장돼 있어도 시작 시 닫힌 상태를 보장하되,
        // 방금 Open()이 유발한 Awake라면 닫지 않는다(CLAUDE.md의 _isOpen 가드).
        if (!_isOpen)
        {
            CloseSilently();
        }
    }

    private void OnDestroy()
    {
        if (_proceedButton != null)
        {
            _proceedButton.onClick.RemoveListener(ProceedToTutorial);
        }

        if (_skipButton != null)
        {
            _skipButton.onClick.RemoveListener(SkipToGame);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.RemoveListener(Close);
        }
    }

    private void OnEnable()
    {
        if (_closeAction != null)
        {
            // 액션을 켜는 것은 GlobalInputBootstrap의 몫이다(UI_LoadGameWindow와 같은 판단).
            _closeAction.action.performed += OnCloseActionPerformed;
        }
    }

    private void OnDisable()
    {
        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }
    }

    /// <summary>건너뛰기로 열 씬을 지정한다. 타이틀 화면이 한 번 호출한다.</summary>
    public void Construct(string gameSceneName)
    {
        _gameSceneName = gameSceneName;
    }

    public void Open()
    {
        SoundManager.Play(SoundId.UiWindowOpen);

        _isOpen = true;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        SoundManager.Play(SoundId.UiWindowClose);
        CloseSilently();
    }

    public void ProceedToTutorial()
    {
        LoadSceneOnce(SceneNames.TUTORIAL);
    }

    public void SkipToGame()
    {
        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[UI_TutorialPromptPanel] 건너뛰기로 열 씬이 지정되지 않았습니다. Construct 호출을 확인하세요.", this);
            return;
        }

        LoadSceneOnce(_gameSceneName);
    }

    // Awake의 자기 닫기가 창 닫는 소리를 내지 않게 소리 없는 경로를 따로 둔다.
    // 이 창은 씬에 활성으로 저장돼 있어 Awake가 씬 로드 때마다 반드시 한 번 닫는다.
    private void CloseSilently()
    {
        _isOpen = false;
        gameObject.SetActive(false);
    }

    // 라벨 key를 인스펙터가 아니라 코드에서 넣는다 - 씬에만 있는 key는 상수와 조용히 어긋난다.
    private void ApplyLabelKeys()
    {
        if (_messageLabel != null)
        {
            _messageLabel.SetKey(TitleLocKeys.TUTORIAL_PROMPT_MESSAGE);
        }

        if (_proceedLabel != null)
        {
            _proceedLabel.SetKey(TitleLocKeys.TUTORIAL_PROMPT_PROCEED);
        }

        if (_skipLabel != null)
        {
            _skipLabel.SetKey(TitleLocKeys.TUTORIAL_PROMPT_SKIP);
        }
    }

    private void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        Close();
    }

    private void LoadSceneOnce(string sceneName)
    {
        if (_hasRequested)
        {
            return;
        }

        _hasRequested = true;
        SoundManager.Play(SoundId.UiButtonClick);
        SceneManager.LoadScene(sceneName);
    }
}
