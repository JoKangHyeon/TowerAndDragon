using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 설정 창(Config_window) 컨트롤러. 좌측 패널(Left)의 그래픽·사운드·언어 항목을
/// SettingsService에 연결한다. 값의 저장·적용은 전부 서비스가 하고, 이 창은 표시와 입력만 맡는다.
/// 창 루트(전면 Image)에 부착해 바깥 클릭 닫기와 IExclusiveMode(UIManager.OpenExclusive) 조정을
/// 겸한다 — UI_ResearchWindow와 같은 구조.
/// 고정 라벨(섹션 제목 등)은 코드가 아니라 LocalizedText 컴포넌트로 번역한다.
/// </summary>
public class UI_ConfigWindow : MonoBehaviour, IExclusiveMode
{
    // stringtable — config_resolution_format : "{0}x{1}"
    private const string RESOLUTION_FORMAT_LOC_KEY = "config_resolution_format";
    // stringtable — 언어 드롭다운에 보여줄 언어 이름.
    private const string LANGUAGE_NAME_EN_US_LOC_KEY = "config_language_en_us";
    private const string LANGUAGE_NAME_KO_KR_LOC_KEY = "config_language_ko_kr";

    private const string LANGUAGE_CODE_KO_KR = "ko_kr";

    // Prev/Next 버튼이 해상도 인덱스를 움직이는 방향.
    private const int STEP_PREVIOUS = -1;
    private const int STEP_NEXT = 1;

    [Header("Dependencies")]
    [SerializeField] private SettingsService _settings;
    [SerializeField] private UIManager _uiManager;
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

    [Header("저장 / 불러오기")]
    [Tooltip("슬롯 창을 저장 모드로 여는 버튼.")]
    [SerializeField] private Button _saveButton;
    [Tooltip("슬롯 창을 불러오기 모드로 여는 버튼.")]
    [SerializeField] private Button _loadButton;
    [Tooltip("두 버튼이 공유하는 세이브 슬롯 목록 창.")]
    [SerializeField] private UI_LoadGameWindow _slotWindow;
    [Tooltip("두 버튼을 담은 줄(SaveLoad_Button). 버튼이 전부 빠질 때 줄째로 접기 위해 받는다.")]
    [SerializeField] private GameObject _slotButtonRow;

    private readonly List<string> _languageOptionBuffer = new();

    // 사운드 줄은 자식에서 모아 쓴다 - 줄이 늘어도 창 쪽 배선을 고칠 필요가 없다.
    private UI_VolumeRow[] _volumeRows;

    private bool _isOpen;

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

        if (_saveButton != null)
        {
            _saveButton.onClick.AddListener(OpenSaveWindow);
        }

        if (_loadButton != null)
        {
            _loadButton.onClick.AddListener(OpenLoadWindow);
        }

        // 단순 setter라 슬롯 창의 Awake보다 앞서도 안전하다(UI_TitleWindow의 Construct와 같은 처리).
        if (_slotWindow != null)
        {
            _slotWindow.Construct(_saveService);
        }

        RenderSlotButtons();

        _volumeRows = GetComponentsInChildren<UI_VolumeRow>(true);
        foreach (UI_VolumeRow row in _volumeRows)
        {
            row.Construct(_settings, _volumeStep);
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

        // 구독 직후 현재 값을 한 번 반영해 초기 발화를 놓쳐도 안전하게 한다.
        Render();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= Render;

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }
    }

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

    private void Render()
    {
        RenderVolumes();
        RenderResolution();
        RenderFullScreen();
        RenderLanguage();
        RenderSaveAvailability();
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

        _languageOptionBuffer.Clear();
        foreach (string languageCode in languages)
        {
            _languageOptionBuffer.Add(LanguageDisplayName(languageCode));
        }

        _languageDropdown.ClearOptions();
        _languageDropdown.AddOptions(_languageOptionBuffer);
        _languageDropdown.SetValueWithoutNotify(_settings.LanguageIndex);
        _languageDropdown.RefreshShownValue();
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
}
