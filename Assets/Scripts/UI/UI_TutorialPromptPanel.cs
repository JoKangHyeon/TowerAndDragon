using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 새 게임을 누른 뒤 "튜토리얼을 진행하시겠습니까?"를 묻는 창(StartScene의 TutorialBridge_window).
/// 진행이면 튜토리얼 씬, 건너뛰기면 본게임 씬으로 간다.
///
/// 어느 쪽으로 가도 정적 상태를 세팅하지 않는 것이 계약이다 - <see cref="SaveLoadRequest"/>가 비어 있어야
/// 게임 씬의 GameManager가 새 런으로 시작한다(<see cref="SceneNames"/>, TutorialToGameHandoff와 같은 판단).
/// 비우는 일은 이 창을 여는 UI_TitleWindow.StartNewGame이 이미 한다.
///
/// 묻는 것은 새 게임뿐이다. 이어하기·불러오기는 이 창을 거치지 않으므로 "튜토리얼을 봤는가" 같은
/// 저장 상태가 필요 없다.
///
/// 이 스크립트는 창 루트에 붙고, 바깥 클릭 닫기는 자식 Panel의 전면 dim Image + Button이 맡는다
/// (UI_LoadGameWindow와 같은 구조를 부모/자식으로 나눈 형태). 박스(Select_window)에는 UI_ClickBlocker가
/// 붙어 있어야 한다 - Unity는 클릭을 부모로 올려 보내므로, 없으면 박스 안쪽을 눌러도 창이 닫힌다.
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

    [Tooltip("씬을 여는 동안 화면을 덮는 로딩 화면. 진행(튜토리얼)·건너뛰기(본게임) 양쪽 다 이 인스턴스를 쓴다. " +
             "비어 있으면 로딩 화면 없이 바로 넘어간다.")]
    [SerializeField] private SceneLoadOverlay _loadOverlay;

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

        // 창이 씬에 비활성으로 저장돼 있으므로, 이 Awake는 첫 Open()의 SetActive(true) 안에서 동기 실행된다.
        // Open()이 SetActive보다 먼저 _isOpen을 세우기 때문에 여기서 자기를 닫지 않는다 -
        // 이 가드를 빼면 첫 클릭이 스스로를 닫아 두 번째부터 열린다(CLAUDE.md의 _isOpen 가드).
        // 아래 분기는 누가 창을 활성으로 저장했을 때만 도는 안전망이다.
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
        // 씬 로드를 이미 요청했으면 닫지 않는다. Esc는 전역 InputAction이라 로딩 오버레이의
        // blocksRaycasts가 막지 못하는데, 그대로 닫으면 로딩 중에 창 닫는 소리가 나고
        // 패널만 오버레이 뒤에서 사라진다(씬 전환은 그대로 진행된다).
        if (_hasRequested)
        {
            return;
        }

        SoundManager.Play(SoundId.UiWindowClose);
        CloseSilently();
    }

    // 씬 콜드 진입에 3.8초가 걸려(측정치) 그동안 화면이 얼어붙는다. 진행·건너뛰기 둘 다 같은
    // 오버레이 인스턴스로 로딩 화면을 덮는다.
    public void ProceedToTutorial()
    {
        if (!TryBeginSceneRequest())
        {
            return;
        }

        SceneLoadOverlay.LoadOrFallback(_loadOverlay, SceneNames.TUTORIAL, nameof(_loadOverlay), this);
    }

    public void SkipToGame()
    {
        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[UI_TutorialPromptPanel] 건너뛰기로 열 씬이 지정되지 않았습니다. Construct 호출을 확인하세요.", this);
            return;
        }

        if (!TryBeginSceneRequest())
        {
            return;
        }

        SceneLoadOverlay.LoadOrFallback(_loadOverlay, _gameSceneName, nameof(_loadOverlay), this);
    }

    // Awake의 안전망 닫기가 창 닫는 소리를 내지 않게 소리 없는 경로를 따로 둔다.
    // 창을 활성으로 저장하면 그 Awake가 씬 로드 때 도는데, 그때 Close()를 부르면 타이틀 진입 직후
    // 닫는 소리가 난다.
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

    // 씬 로드는 되돌릴 수 없으므로 첫 요청만 통과시킨다. 진행·건너뛰기가 각자 다른 방식으로 씬을 열어도
    // 가드는 하나를 공유해야 한다 - 한쪽을 누른 뒤 다른 쪽을 눌러도 두 번 나가지 않도록.
    private bool TryBeginSceneRequest()
    {
        if (_hasRequested)
        {
            return false;
        }

        _hasRequested = true;
        SoundManager.Play(SoundId.UiButtonClick);
        return true;
    }
}
