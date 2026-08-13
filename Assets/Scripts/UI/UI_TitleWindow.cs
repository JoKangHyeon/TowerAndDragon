using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 시작 화면(StartScene) 컨트롤러. 메뉴 버튼 5개를 각 진입점에 연결한다.
/// UI_ConfigWindow와 같은 구조 - 싱글톤 없이 [SerializeField] 주입, 배선은 Awake에서 코드로 한다.
///
/// 새 게임과 이어하기의 차이는 SaveLoadRequest뿐이다. 요청을 남기면 게임 씬의
/// SaveService.Awake가 소비하고 GameManager.Start가 복원 경로로, 없으면 StartNewRun으로 간다.
/// 여기서 세이브를 직접 읽거나 게임 상태를 만들지 않는다.
///
/// 새 게임만 튜토리얼 여부를 묻는다 - 이어하기·불러오기는 UI_TutorialPromptPanel을 거치지 않으므로
/// "튜토리얼을 봤는가"를 어디에도 저장하지 않아도 된다.
/// </summary>
public class UI_TitleWindow : MonoBehaviour
{
    [Header("메뉴 버튼")]
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _loadButton;
    [SerializeField] private Button _configButton;
    [SerializeField] private Button _quitButton;

    [Header("창")]
    [SerializeField] private UI_ConfigWindow _configWindow;
    [SerializeField] private UI_LoadGameWindow _loadGameWindow;

    [Tooltip("새 게임을 누르면 여는 튜토리얼 진행 여부 확인 창.")]
    [SerializeField] private UI_TutorialPromptPanel _tutorialPromptPanel;

    [Header("씬")]
    [Tooltip("새 게임·이어하기로 진입할 씬. Build Settings에 등록돼 있어야 한다.")]
    [SerializeField] private string _gameSceneName = SceneNames.SAMPLE_GAME;

    private void Awake()
    {
        if (_newGameButton != null)
        {
            _newGameButton.onClick.AddListener(StartNewGame);
        }

        if (_continueButton != null)
        {
            _continueButton.onClick.AddListener(ContinueMostRecent);
        }

        if (_loadButton != null)
        {
            _loadButton.onClick.AddListener(OpenLoadWindow);
        }

        if (_configButton != null)
        {
            _configButton.onClick.AddListener(ToggleConfigWindow);
        }

        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(QuitGame);
        }

        if (_loadGameWindow != null)
        {
            _loadGameWindow.Construct(_gameSceneName);
        }

        if (_tutorialPromptPanel != null)
        {
            _tutorialPromptPanel.Construct(_gameSceneName);
        }
    }

    // 세이브 유무 판정과 BGM은 Start에서 한다(CLAUDE.md 이벤트 초기화 규칙 - 첫 발화는 Awake가 아니다).
    private void Start()
    {
        SoundManager.PlayBgm(BgmId.Title);
        RenderSaveDependentButtons();
    }

    private void RenderSaveDependentButtons()
    {
        // 버튼에 붙은 UI_ButtonInteractableFade가 interactable을 따라 투명도를 낮춰 준다.
        bool hasAnySave = SaveSlotQuery.HasAnySave;

        if (_continueButton != null)
        {
            _continueButton.interactable = hasAnySave;
        }

        if (_loadButton != null)
        {
            _loadButton.interactable = hasAnySave;
        }
    }

    // 여는 소리는 UI_TutorialPromptPanel.Open()이 낸다 - 여기서 또 내면 겹친다(OpenLoadWindow와 같은 이유).
    private void StartNewGame()
    {
        // 이전에 눌렀던 이어하기 요청이 소비되지 않고 남아 있을 수 있다.
        // 튜토리얼로 가든 본게임으로 가든 요청이 비어 있어야 하므로, 창을 열기 전에 지운다.
        SaveLoadRequest.Clear();

        if (_tutorialPromptPanel == null)
        {
            Debug.LogWarning("[UI_TitleWindow] 튜토리얼 확인 창이 없어 본게임으로 바로 넘어갑니다.", this);
            SoundManager.Play(SoundId.UiButtonClick);
            LoadGameScene();
            return;
        }

        _tutorialPromptPanel.Open();
    }

    private void ContinueMostRecent()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        int slotIndex = SaveSlotQuery.MostRecentSlotIndex;
        if (!SavePaths.IsValidSlotIndex(slotIndex))
        {
            Debug.LogWarning("[UI_TitleWindow] 이어할 세이브가 없습니다.");
            return;
        }

        SaveLoadRequest.Request(slotIndex);
        LoadGameScene();
    }

    // 여는 소리는 UI_LoadGameWindow.Open()이 낸다 - 여기서 또 내면 겹친다.
    private void OpenLoadWindow()
    {
        if (_loadGameWindow != null)
        {
            _loadGameWindow.Open();
        }
    }

    // 여닫는 소리는 UI_ConfigWindow.Open()/Close()가 낸다 - 여기서 또 내면 겹친다.
    private void ToggleConfigWindow()
    {
        if (_configWindow != null)
        {
            _configWindow.ToggleFromEntryPoint();
        }
    }

    private void QuitGame()
    {
        SoundManager.Play(SoundId.UiButtonClick);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadGameScene()
    {
        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[UI_TitleWindow] 게임 씬 이름이 비어 있습니다.");
            return;
        }

        SceneManager.LoadScene(_gameSceneName);
    }
}
