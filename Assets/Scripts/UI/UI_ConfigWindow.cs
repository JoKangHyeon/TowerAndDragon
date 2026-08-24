using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 설정 창(Config_window) 컨트롤러. 좌측 패널(Left)의 그래픽·사운드·언어·튜토리얼 항목을
/// SettingsService에 연결한다. 값의 저장·적용은 전부 서비스가 하고, 이 창은 표시와 입력만 맡는다.
/// 창 루트(전면 Image)에 부착해 바깥 클릭 닫기와 IExclusiveMode(UIManager.OpenExclusive) 조정을
/// 겸한다 — UI_ResearchWindow와 같은 구조.
/// 열려 있는 동안에는 게임이 멈춘다(GameSpeedManager의 창 정지). 아무 창도 열려 있지 않을 때
/// Esc로 이 창을 여는 것은 UIManager가 맡는다 — 닫혀 있는 창은 스스로 입력을 받을 수 없다.
/// 고정 라벨(섹션 제목 등)은 코드가 아니라 LocalizedText 컴포넌트로 번역한다.
/// </summary>
public class UI_ConfigWindow : MonoBehaviour, IExclusiveMode
{
    // stringtable — config_resolution_format : "{0}x{1}"
    private const string RESOLUTION_FORMAT_LOC_KEY = "config_resolution_format";
    // stringtable — 언어 드롭다운에 보여줄 언어 이름.
    private const string LANGUAGE_NAME_EN_US_LOC_KEY = "config_language_en_us";
    private const string LANGUAGE_NAME_KO_KR_LOC_KEY = "config_language_ko_kr";
    // stringtable — 튜토리얼 안내 드롭다운에 보여줄 수준 이름.
    private const string GUIDE_LEVEL_FULL_LOC_KEY = "config_guide_full";
    private const string GUIDE_LEVEL_OFF_LOC_KEY = "config_guide_off";

    // stringtable — 되돌릴 수 없는 조작 직전에 물어보는 확인 문구.
    // 버튼 라벨 쪽 key는 코드가 아니라 프리팹의 LocalizedText가 갖는다
    // (config_button_save·config_button_load와 같은 처리).
    private const string RETURN_TO_TITLE_CONFIRM_LOC_KEY = "config_confirm_return_to_title";
    private const string QUIT_GAME_CONFIRM_LOC_KEY = "config_confirm_quit_game";

    private const string LANGUAGE_CODE_KO_KR = "ko_kr";

    // Prev/Next 버튼이 해상도 인덱스를 움직이는 방향.
    private const int STEP_PREVIOUS = -1;
    private const int STEP_NEXT = 1;

    // ScrollRect의 세로 위치는 0이 맨 아래, 1이 맨 위다.
    private const float SCROLL_TOP = 1f;

    [Header("Dependencies")]
    [SerializeField] private SettingsService _settings;
    [Tooltip("이 창을 소유한 인게임 UIManager. 비어 있다는 것이 곧 타이틀 화면 인스턴스라는 표시다" +
        "(IsTitleScreenInstance) - 게임 속도 제어와 배타 열기, 튜토리얼 안내 줄이 함께 꺼진다.")]
    [WiringOptional]
    [SerializeField] private UIManager _uiManager;
    [Tooltip("저장 가능 위상 판정과 슬롯 창 주입에 쓴다. 타이틀 화면 인스턴스에는 저장할 게임 상태가 없어 비워 둔다 - " +
        "비우면 저장 버튼이 줄에서 사라진다(불러오기는 남는다).")]
    [WiringOptional]
    [SerializeField] private SaveService _saveService;

    [Header("창 닫기")]
    [Tooltip("우상단 닫기 버튼.")]
    [SerializeField] private Button _closeButton;
    [Tooltip("창 바깥(전면 dim) 클릭으로 닫기. 보통 이 창 루트의 Button.")]
    [SerializeField] private Button _blockerButton;
    [Tooltip("창을 닫는 키 - 보통 Esc.")]
    [SerializeField] private InputActionReference _closeAction;

    [Header("그래픽 - 해상도")]
    [SerializeField] private TMP_Text _resolutionText;
    [SerializeField] private Button _resolutionPrevButton;
    [SerializeField] private Button _resolutionNextButton;

    [Header("그래픽 - 전체화면")]
    [SerializeField] private Toggle _fullScreenToggle;

    [Header("사운드")]
    [Tooltip("화살표 버튼 한 번에 움직일 볼륨 폭(0~1 기준). 자식의 UI_VolumeRow 전부에 적용된다.")]
    [SerializeField] private float _volumeStep = 0.1f;

    [Header("언어")]
    [SerializeField] private TMP_Dropdown _languageDropdown;
    [Tooltip("드롭다운을 열지 않고 이전 언어로 넘기는 화살표.")]
    [SerializeField] private Button _languagePrevButton;
    [Tooltip("드롭다운을 열지 않고 다음 언어로 넘기는 화살표.")]
    [SerializeField] private Button _languageNextButton;

    [Header("튜토리얼")]
    [Tooltip("가이드 안내를 보여줄지 정하는 드롭다운. 조언자 카드와 같은 값(SettingsService.Guide)을 다룬다.")]
    [SerializeField] private TMP_Dropdown _guideDropdown;
    [Tooltip("드롭다운을 열지 않고 이전 수준으로 넘기는 화살표.")]
    [SerializeField] private Button _guidePrevButton;
    [Tooltip("드롭다운을 열지 않고 다음 수준으로 넘기는 화살표.")]
    [SerializeField] private Button _guideNextButton;
    [Tooltip("튜토리얼 섹션 제목(Guide_Header). 타이틀 화면에서는 줄째 접는다.")]
    [SerializeField] private GameObject _guideHeader;
    [Tooltip("튜토리얼 안내 드롭다운 줄(Guide_Panel).")]
    [SerializeField] private GameObject _guidePanel;

    [Header("키")]
    [Tooltip("우측 키 섹션. 줄은 이 컴포넌트가 액션 목록을 훑어 스스로 만든다.")]
    [SerializeField] private UI_KeyBindingSection _keySection;

    [Header("저장 / 불러오기")]
    [Tooltip("슬롯 창을 저장 모드로 여는 버튼.")]
    [SerializeField] private Button _saveButton;
    [Tooltip("슬롯 창을 불러오기 모드로 여는 버튼.")]
    [SerializeField] private Button _loadButton;
    [Tooltip("두 버튼이 공유하는 세이브 슬롯 목록 창. 타이틀 화면 인스턴스에는 없다 - " +
        "비우면 저장/불러오기 줄이 통째로 접힌다.")]
    [WiringOptional]
    [SerializeField] private UI_LoadGameWindow _slotWindow;
    [Tooltip("두 버튼을 담은 줄(SaveLoad_Button). 버튼이 전부 빠질 때 줄째로 접기 위해 받는다.")]
    [SerializeField] private GameObject _slotButtonRow;

    [Header("메인으로 / 종료")]
    [Tooltip("진행 중인 게임을 버리고 메인 화면(StartScene)으로 나가는 버튼.")]
    [SerializeField] private Button _returnToTitleButton;
    [Tooltip("위 버튼을 담은 레이아웃 칸(ReturnToTitle_Button). 줄이 ForceExpandWidth라 안쪽 버튼만 끄면 " +
        "바깥 칸이 남아 줄 절반이 빈 구멍이 된다 - 이 버튼만 접을 때는 칸째로 꺼야 한다.")]
    [SerializeField] private GameObject _returnToTitleItem;
    [Tooltip("게임을 끄는 버튼.")]
    [SerializeField] private Button _quitGameButton;
    [Tooltip("두 버튼을 담은 줄(System_Buttons). 타이틀 화면 인스턴스에서 줄째로 접기 위해 받는다.")]
    [SerializeField] private GameObject _systemButtonRow;
    [Tooltip("두 버튼이 공유하는 YES/NO 확인 팝업. 이 창의 자식이라 창을 끄면 화면에서 같이 사라진다.")]
    [SerializeField] private UI_ConfirmPopup _confirmPopup;

    [Header("좌측 목록")]
    [Tooltip("그래픽·사운드·언어 항목이 든 좌측 스크롤(Setting_Scroll). 창을 열 때마다 맨 위로 되돌리려고 받는다.")]
    [SerializeField] private ScrollRect _settingScroll;

    private readonly List<string> _dropdownOptionBuffer = new();

    // 사운드 줄은 자식에서 모아 쓴다 - 줄이 늘어도 창 쪽 배선을 고칠 필요가 없다.
    private UI_VolumeRow[] _volumeRows;

    private bool _isOpen;

    // 씬 로드는 되돌릴 수 없으므로 두 번 눌려도 한 번만 나간다(UI_TutorialQuitPanel과 같은 방어).
    private bool _hasRequestedSceneChange;

    private void Awake()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.AddListener(Close);
        }

        if (_resolutionPrevButton != null)
        {
            _resolutionPrevButton.onClick.AddListener(() => StepResolution(STEP_PREVIOUS));
        }

        if (_resolutionNextButton != null)
        {
            _resolutionNextButton.onClick.AddListener(() => StepResolution(STEP_NEXT));
        }

        if (_fullScreenToggle != null)
        {
            _fullScreenToggle.onValueChanged.AddListener(HandleFullScreenChanged);
        }

        if (_languageDropdown != null)
        {
            _languageDropdown.onValueChanged.AddListener(HandleLanguageChanged);
        }

        if (_languagePrevButton != null)
        {
            _languagePrevButton.onClick.AddListener(() => StepLanguage(STEP_PREVIOUS));
        }

        if (_languageNextButton != null)
        {
            _languageNextButton.onClick.AddListener(() => StepLanguage(STEP_NEXT));
        }

        if (_guideDropdown != null)
        {
            _guideDropdown.onValueChanged.AddListener(HandleGuideLevelChanged);
        }

        if (_guidePrevButton != null)
        {
            _guidePrevButton.onClick.AddListener(() => StepGuideLevel(STEP_PREVIOUS));
        }

        if (_guideNextButton != null)
        {
            _guideNextButton.onClick.AddListener(() => StepGuideLevel(STEP_NEXT));
        }

        if (_saveButton != null)
        {
            _saveButton.onClick.AddListener(OpenSaveWindow);
        }

        if (_loadButton != null)
        {
            _loadButton.onClick.AddListener(OpenLoadWindow);
        }

        if (_returnToTitleButton != null)
        {
            _returnToTitleButton.onClick.AddListener(ConfirmReturnToTitle);
        }

        if (_quitGameButton != null)
        {
            _quitGameButton.onClick.AddListener(ConfirmQuitGame);
        }

        // 단순 setter라 슬롯 창의 Awake보다 앞서도 안전하다(UI_TitleWindow의 Construct와 같은 처리).
        if (_slotWindow != null)
        {
            _slotWindow.Construct(_saveService);
        }

        RenderSlotButtons();
        RenderGuideSection();
        RenderSystemButtons();

        _volumeRows = GetComponentsInChildren<UI_VolumeRow>(true);
        foreach (UI_VolumeRow row in _volumeRows)
        {
            row.Construct(_settings, _volumeStep);
        }

        // 키 섹션도 같은 방식으로 주입한다 - SettingsService는 씬 참조라 프리팹 안에서 배선할 수 없다.
        if (_keySection != null)
        {
            _keySection.Construct(_settings);
        }
    }

    private void OnEnable()
    {
        // 언어가 바뀌면 드롭다운 항목 이름과 퍼센트·해상도 표기를 다시 그린다.
        StringTable.OnLanguageChanged += Render;

        if (_closeAction != null)
        {
            _closeAction.action.performed += OnCloseActionPerformed;
        }

        PauseGameWhileOpen();

        // 구독 직후 현재 값을 한 번 반영해 초기 발화를 놓쳐도 안전하게 한다.
        Render();

        ScrollListToTop();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= Render;

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }

        // 확인 팝업도 같이 접는다. 팝업은 이 창의 자식이라 창을 끄면 화면에서는 사라지지만
        // activeSelf가 그대로 남아, 창을 다시 열면 지난번 확인 창이 떠 있는 것처럼 보인다
        // (UI_LoadGameWindow.OnDisable과 같은 처리).
        if (_confirmPopup != null && _confirmPopup.IsOpen)
        {
            _confirmPopup.Close();
        }

        ResumeGameOnClose();
    }

    // 창이 열려 있는 동안 게임을 멈춘다. 여닫는 경로가 여럿이라(Esc·닫기 버튼·바깥 클릭·
    // 다른 배타 모드 열기·슬롯 창으로 전환) Open/Close가 아니라 짝이 보장되는 OnEnable/OnDisable에 건다.
    //
    // GameSpeedManager의 창 정지는 플레이어가 건 일시정지와 별개로 쌓이므로, 밤에 정지해 둔 채로
    // 설정 창을 열었다 닫아도 정지 상태가 그대로 남는다.
    private void PauseGameWhileOpen()
    {
        GameSpeedManager gameSpeed = GameSpeed;
        if (gameSpeed != null)
        {
            gameSpeed.AddWindowPause();
        }
    }

    private void ResumeGameOnClose()
    {
        GameSpeedManager gameSpeed = GameSpeed;
        if (gameSpeed != null)
        {
            gameSpeed.RemoveWindowPause();
        }
    }

    // 같은 프리팹을 타이틀 화면과 인게임이 나눠 쓴다. 타이틀 화면 인스턴스에는 UIManager도
    // 게임도 없어 null이다 - 인게임 전용 항목을 접을지 여기 한 곳에서 판정한다.
    private bool IsTitleScreenInstance => _uiManager == null;

    // 멈출 게임이 없는 타이틀 화면에서는 null이다.
    private GameSpeedManager GameSpeed => _uiManager != null ? _uiManager.GameSpeed : null;

    public void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        Close();
    }

    public void Open()
    {
        SoundManager.Play(SoundId.UiWindowOpen);

        _isOpen = true;
        gameObject.SetActive(true);
        Render();
    }

    public void Close()
    {
        SoundManager.Play(SoundId.UiWindowClose);

        _isOpen = false;
        gameObject.SetActive(false);

        // 창을 닫는 시점에 한 번만 디스크에 기록한다(슬라이더를 끌 때마다 쓰지 않도록).
        if (_settings != null)
        {
            _settings.Save();
        }
    }

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => Open();
    void IExclusiveMode.Close() => Close();

    // HUD 설정(톱니) 버튼이 호출하는 진입점.
    public void ToggleFromEntryPoint()
    {
        if (_isOpen)
        {
            Close();
        }
        else if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            Open();
        }
    }

    private void StepResolution(int direction)
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (!WiringGuard.Require(_settings, nameof(_settings), this))
        {
            return;
        }

        _settings.SetResolutionIndex(_settings.ResolutionIndex + direction);
        RenderResolution();
    }

    private void HandleFullScreenChanged(bool isFullScreen)
    {
        if (_settings != null)
        {
            _settings.SetFullScreen(isFullScreen);
        }
    }

    // 언어를 바꾸면 OnLanguageChanged → Render로 드롭다운 선택도 따라오므로, 여기선 인덱스만 넘긴다.
    private void StepLanguage(int direction)
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_settings != null)
        {
            _settings.SetLanguageIndex(_settings.LanguageIndex + direction);
        }
    }

    private void HandleLanguageChanged(int index)
    {
        // 언어를 바꾸면 StringTable이 OnLanguageChanged를 발화하고, 그 구독으로 Render가 다시 돌아간다.
        if (_settings != null)
        {
            _settings.SetLanguageIndex(index);
        }
    }

    // 언어와 달리 값이 바뀌어도 이 창을 다시 그리게 하는 전역 이벤트가 없으므로 직접 다시 그린다
    // (해상도 화살표와 같은 처리).
    private void StepGuideLevel(int direction)
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_settings == null)
        {
            return;
        }

        _settings.SetGuideLevelIndex(_settings.GuideLevelIndex + direction);
        RenderGuideLevel();
    }

    private void HandleGuideLevelChanged(int index)
    {
        if (_settings != null)
        {
            _settings.SetGuideLevelIndex(index);
        }
    }

    // 좌측 목록은 뷰포트보다 길어 스크롤된다. 프리팹에 저장된 스크롤 위치가 맨 아래라 그대로 두면
    // 창을 열 때마다 목록의 끝(마지막 버튼 줄)이 잘린 채로 먼저 보인다 - 설정 목록은 늘 위에서부터 읽는다.
    // 스크롤 위치는 뷰포트와 내용의 높이 비로 계산되므로, 레이아웃이 이번 프레임에 다시 잡힌 뒤에 넣어야 한다.
    private void ScrollListToTop()
    {
        if (_settingScroll == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        _settingScroll.verticalNormalizedPosition = SCROLL_TOP;
    }

    private void Render()
    {
        RenderVolumes();
        RenderResolution();
        RenderFullScreen();
        RenderLanguage();
        RenderGuideLevel();
        RenderKeyBindings();
        RenderSaveAvailability();
    }

    // 키 섹션은 자기 OnEnable에서도 한 번 그리지만, 언어가 바뀐 뒤에는 창이 켜진 채로 다시 그려야 한다.
    private void RenderKeyBindings()
    {
        if (_keySection != null)
        {
            _keySection.Render();
        }
    }

    // 저장은 낮에만 가능하다(SaveService 계약 2). 눌러 봐야 슬롯이 전부 잠긴 창만 열리는 버튼을
    // 살려 두지 않는다 - UI_BuildModeWindow와 같은 처리다. 흐려지는 것은 버튼에 붙은
    // UI_ButtonInteractableFade가 맡으므로 여기서 색을 직접 건드리지 않는다.
    // 위상 판정을 CycleManager에서 다시 하지 않고 SaveService에 위임한다 - 판정이 두 벌로
    // 갈라지지 않고, 새 SerializeField 배선을 늘리지 않는다. CanSave가 아니라 IsSaveablePhase를
    // 보는 이유는 그쪽 주석에 있다(이 창은 열 때 한 번만 판정한다).
    private void RenderSaveAvailability()
    {
        if (_saveButton != null)
        {
            _saveButton.interactable = _saveService != null && _saveService.IsSaveablePhase;
        }
    }

    private void RenderVolumes()
    {
        if (_volumeRows == null)
        {
            return;
        }

        foreach (UI_VolumeRow row in _volumeRows)
        {
            row.Render();
        }
    }

    private void RenderResolution()
    {
        if (_resolutionText == null || _settings == null || _settings.Resolutions.Count == 0)
        {
            return;
        }

        Vector2Int resolution = _settings.Resolutions[_settings.ResolutionIndex];
        _resolutionText.text = string.Format(
            StringTable.GetString(RESOLUTION_FORMAT_LOC_KEY), resolution.x, resolution.y);
    }

    private void RenderFullScreen()
    {
        if (_fullScreenToggle != null && _settings != null)
        {
            _fullScreenToggle.SetIsOnWithoutNotify(_settings.IsFullScreen);
        }
    }

    private void RenderLanguage()
    {
        if (_languageDropdown == null || _settings == null)
        {
            return;
        }

        IReadOnlyList<string> languages = _settings.Languages;
        if (languages == null || languages.Count == 0)
        {
            return;
        }

        _dropdownOptionBuffer.Clear();
        foreach (string languageCode in languages)
        {
            _dropdownOptionBuffer.Add(LanguageDisplayName(languageCode));
        }

        _languageDropdown.ClearOptions();
        _languageDropdown.AddOptions(_dropdownOptionBuffer);
        _languageDropdown.SetValueWithoutNotify(_settings.LanguageIndex);
        _languageDropdown.RefreshShownValue();
    }

    // 안내 수준은 조언자 카드에서도 바뀐다. 그쪽 변경은 이 창이 닫혀 있을 때만 일어나므로
    // OnGuideLevelChanged를 구독하지 않고, 창을 열 때마다 도는 Render로 현재 값을 반영한다.
    private void RenderGuideLevel()
    {
        if (_guideDropdown == null || _settings == null)
        {
            return;
        }

        IReadOnlyList<GuideLevel> levels = _settings.GuideLevels;

        _dropdownOptionBuffer.Clear();
        foreach (GuideLevel level in levels)
        {
            _dropdownOptionBuffer.Add(StringTable.GetString(GuideLevelNameLocKey(level)));
        }

        _guideDropdown.ClearOptions();
        _guideDropdown.AddOptions(_dropdownOptionBuffer);
        _guideDropdown.SetValueWithoutNotify(_settings.GuideLevelIndex);
        _guideDropdown.RefreshShownValue();
    }

    // 언어 이름과 같은 이유로 문자열 조합을 쓰지 않고 아는 값만 명시적으로 매핑한다.
    // GuideLevel에 수준이 늘면 여기도 늘려야 한다 - 빠뜨리면 "전체"로 보인다.
    private static string GuideLevelNameLocKey(GuideLevel level)
    {
        return level switch
        {
            GuideLevel.Off => GUIDE_LEVEL_OFF_LOC_KEY,
            _ => GUIDE_LEVEL_FULL_LOC_KEY,
        };
    }

    private static string LanguageDisplayName(string languageCode)
    {
        string locKey = LanguageNameLocKey(languageCode);

        // 스트링테이블에 이름이 없는 언어는 코드를 그대로 보여준다(빈칸보다 낫다).
        return locKey == null ? languageCode : StringTable.GetString(locKey);
    }

    // 언어 목록은 폴더 스캔 결과라 런타임에 늘어날 수 있다. 키를 문자열 조합으로 만들지 않고
    // 아는 언어만 명시적으로 매핑해, 오타·미등록 키가 코드에서 바로 보이게 한다.
    private static string LanguageNameLocKey(string languageCode)
    {
        if (languageCode == StringTable.c_DefaultLanguage)
        {
            return LANGUAGE_NAME_EN_US_LOC_KEY;
        }

        if (languageCode == LANGUAGE_CODE_KO_KR)
        {
            return LANGUAGE_NAME_KO_KR_LOC_KEY;
        }

        return null;
    }

    // 슬롯 창은 같은 프리팹을 쓰는 인게임 설정 창에만 배선된다. 타이틀 화면 인스턴스에는 없다.
    private bool HasSlotWindow => _slotWindow != null;

    // 저장은 인게임에서만 가능하다 - 타이틀 화면에는 저장할 게임 상태도 SaveService도 없다.
    private bool CanOfferSave => HasSlotWindow && _saveService != null;

    // 배선은 인스펙터에서 정해지고 런타임에 바뀌지 않으므로 Awake에서 한 번만 반영한다.
    // 눌러도 아무 일이 없는 버튼을 남겨 두면 타이틀 화면에서 로그만 찍히고 끝난다.
    private void RenderSlotButtons()
    {
        if (_saveButton != null)
        {
            _saveButton.gameObject.SetActive(CanOfferSave);
        }

        if (_loadButton != null)
        {
            _loadButton.gameObject.SetActive(HasSlotWindow);
        }

        // 버튼만 끄면 세로 레이아웃에 빈 줄이 남는다. 줄을 끄면 VerticalLayoutGroup이 자리째 거둔다.
        if (_slotButtonRow != null)
        {
            _slotButtonRow.SetActive(HasSlotWindow);
        }
    }

    // 안내 수준은 인게임 조언자 퀘스트를 다루는 설정이라, 게임이 없는 타이틀 화면에서는 의미가 없다.
    // 배선은 인스펙터에서 정해지고 런타임에 바뀌지 않으므로 Awake에서 한 번만 반영한다
    // (RenderSlotButtons와 같은 처리). 헤더와 줄이 세로 레이아웃의 별도 형제라 둘 다 꺼야
    // VerticalLayoutGroup이 자리째 거둔다.
    private void RenderGuideSection()
    {
        bool isVisible = !IsTitleScreenInstance;

        if (_guideHeader != null)
        {
            _guideHeader.SetActive(isVisible);
        }

        if (_guidePanel != null)
        {
            _guidePanel.SetActive(isVisible);
        }
    }

    // 배선은 인스펙터에서 정해지고 런타임에 바뀌지 않으므로 Awake에서 한 번만 반영한다
    // (RenderSlotButtons·RenderGuideSection과 같은 처리).
    //
    // 타이틀 화면 인스턴스에서는 줄째 접는다 - 이미 메인 화면이고, 종료 버튼은 타이틀 메뉴가 따로 갖고 있다.
    // 튜토리얼에서는 "메인 화면으로"만 접는다. 튜토리얼을 벗어나는 길은 UI_TutorialQuitPanel이
    // 본게임으로 넘기는 경로 하나로 정해져 있어 목적지가 다른 두 번째 출구를 두지 않지만,
    // 앱을 끄는 수단은 튜토리얼에도 있어야 한다.
    private void RenderSystemButtons()
    {
        bool isInGame = !IsTitleScreenInstance;

        if (_returnToTitleItem != null)
        {
            _returnToTitleItem.SetActive(isInGame && !SceneNames.IsTutorialScene);
        }

        if (_quitGameButton != null)
        {
            _quitGameButton.gameObject.SetActive(isInGame);
        }

        // 버튼만 끄면 세로 레이아웃에 빈 줄이 남는다. 줄을 끄면 VerticalLayoutGroup이 자리째 거둔다.
        if (_systemButtonRow != null)
        {
            _systemButtonRow.SetActive(isInGame);
        }
    }

    private void OpenSaveWindow() => OpenSlotWindow(UI_LoadGameWindow.WindowMode.Save);

    private void OpenLoadWindow() => OpenSlotWindow(UI_LoadGameWindow.WindowMode.Load);

    // 슬롯 창을 설정 창 위에 겹치지 않고 설정 창 대신 연다. 두 창이 같은 Esc 액션을 구독하고 있어
    // 겹쳐 두면 Esc 한 번에 둘 다 닫히고, Close()가 설정값 저장까지 겸한다.
    private void OpenSlotWindow(UI_LoadGameWindow.WindowMode mode)
    {
        if (_slotWindow == null)
        {
            Debug.LogError("[UI_ConfigWindow] 슬롯 창이 배선되지 않았습니다.");
            return;
        }

        // 여닫는 소리는 각 창의 Open()/Close()가 낸다 - 여기서 또 내면 겹친다.
        Close();

        if (mode == UI_LoadGameWindow.WindowMode.Save)
        {
            _slotWindow.OpenForSave();
        }
        else
        {
            _slotWindow.OpenForLoad();
        }
    }

    private void ConfirmReturnToTitle() => OpenConfirm(RETURN_TO_TITLE_CONFIRM_LOC_KEY, ReturnToTitle);

    private void ConfirmQuitGame() => OpenConfirm(QUIT_GAME_CONFIRM_LOC_KEY, QuitGame);

    // 슬롯 창과 달리 이 창을 닫지 않고 팝업을 그 위에 띄운다. 팝업이 이 창의 자식이라 창을 닫으면
    // 팝업도 같이 꺼지기 때문이고, 팝업 루트의 전면 딤이 바깥 클릭 닫기(_blockerButton)를 대신 막아 준다.
    // 두 창이 같은 Esc 액션을 구독해 겹쳐 둘 수 없었던 슬롯 창의 사정은 여기엔 없다 -
    // UI_ConfirmPopup은 입력 액션을 구독하지 않는다.
    private void OpenConfirm(string messageLocKey, Action onConfirmed)
    {
        SoundManager.Play(SoundId.UiButtonClick);

        // 팝업이 없다고 되돌릴 수 없는 조작을 그냥 실행하지는 않는다 - 물어볼 수 없으면 하지 않는다.
        if (!WiringGuard.Require(_confirmPopup, nameof(_confirmPopup), this))
        {
            return;
        }

        _confirmPopup.Open(messageLocKey, onConfirmed);
    }

    /// <summary>진행 중인 게임을 버리고 메인 화면으로 나간다.</summary>
    // 씬을 열기 전에 Close()를 먼저 부르는 데는 두 가지 이유가 있다.
    //  1. Close()가 설정 저장을 겸한다 - 볼륨을 바꾸자마자 나가도 그 값이 디스크에 남는다.
    //  2. Time.timeScale 복구 순서를 확정한다. timeScale은 GameSpeedManager만 건드리는 값이라
    //     여기서 직접 1로 되돌릴 수 없는데, 창을 연 채로 나가면 씬이 내려갈 때 이 창의 OnDisable
    //     (창 정지 해제 → 현재 배속 값으로 복귀)과 GameSpeedManager.OnDisable(1로 복귀)이
    //     순서 보장 없이 돈다. 밤에 배속을 켜 둔 채 나갔다면 그 배속이 남을 수 있고,
    //     StartScene에는 그것을 되돌릴 GameSpeedManager가 없다. Close()를 먼저 부르면
    //     이 창의 OnDisable이 지금 돌고, 프레임 끝의 씬 교체에서 GameSpeedManager가 마지막으로 1을 쓴다.
    private void ReturnToTitle()
    {
        if (_hasRequestedSceneChange)
        {
            return;
        }

        _hasRequestedSceneChange = true;

        Close();

        SceneManager.LoadScene(SceneNames.START);
    }

    // 에디터에서는 Application.Quit이 아무 일도 하지 않아 확인할 수 없으므로 플레이 모드를 끈다
    // (UI_TitleWindow.QuitGame과 같은 처리). 여기서도 Close()로 설정을 먼저 디스크에 남긴다.
    private void QuitGame()
    {
        Close();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
