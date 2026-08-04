using Cysharp.Threading.Tasks;
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

    [Header("저장 후 메인화면")]
    [SerializeField] private Button _saveAndRetrunButton;

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

        if (_saveAndRetrunButton != null)
        {
            _saveAndRetrunButton.onClick.AddListener(SaveAndReturnToMainscreen);
        }

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
        _isOpen = true;
        gameObject.SetActive(true);
        Render();
    }

    public void Close()
    {
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
        if (_settings == null)
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

    public async void SaveAndReturnToMainscreen()
    {

        //TODO : 세이브 창 만들고, 
        SaveResult result = await _saveService.SaveAsync(
            1,
            false,
            this.GetCancellationTokenOnDestroy());
    }
}
