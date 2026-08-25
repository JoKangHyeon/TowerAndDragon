using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "새 게임 +" 진입 창(StartScene 전용). 뮤테이터를 단계별로 골라 난이도 점수를 확인하고
/// 그 조합으로 새 런을 시작한다.
///
/// 구조의 원본은 <see cref="UI_TutorialPromptPanel"/>이다 - 창 루트에 붙고, 열 씬 이름은
/// <see cref="Construct"/>로 주입받으며(창이 씬 이름을 자기 [SerializeField]로 갖지 않는다 -
/// 진입점이 두 곳이 되면 갈라진다), 씬 로드는 _hasRequested로 한 번만 한다.
/// 목록 만들기는 <see cref="UI_LoadGameWindow"/>의 <see cref="ComponentPool{T}"/> 관용구를 따른다.
///
/// <b>튜토리얼 확인 창을 거치지 않는다.</b> 새 게임 +를 고를 수 있는 사람은 이미 한 판을 끝냈거나
/// 시연용 해금을 쓴 사람이라, 튜토리얼 여부를 물을 이유가 없다.
///
/// <b>UIManager의 배타 모드 배열에 넣지 않는다.</b> 이 창은 StartScene에만 있고 StartScene에는
/// UIManager 자체가 없다(설정창 인스턴스의 _uiManager가 비어 있는 것이 그 판정이다 -
/// UI_ConfigWindow.IsTitleScreenInstance). 배타 모드는 인게임의 건설·인구·점령·새끼용 모드끼리
/// 서로 닫아 주는 장치이고, 이 창은 그중 어느 것과도 같은 화면에 존재하지 않는다.
/// </summary>
public sealed class UI_NewGamePlusWindow : MonoBehaviour
{
    [Tooltip("고를 수 있는 뮤테이터 목록과 프리셋. 표시 순서도 이 에셋이 정한다.")]
    [SerializeField] private RunMutatorCatalogSO _catalog;

    [Header("목록")]
    [Tooltip("뮤테이터 한 줄 프리팹(Slot_Mutator).")]
    [SerializeField] private UI_MutatorSlot _slotPrefab;

    [Tooltip("생성된 줄이 들어갈 부모.")]
    [SerializeField] private Transform _slotContainer;

    [Header("프리셋")]
    [Tooltip("프리셋 버튼 프리팹(Button_MutatorPreset).")]
    [SerializeField] private UI_MutatorPresetButton _presetButtonPrefab;

    [Tooltip("생성된 프리셋 버튼이 들어갈 부모.")]
    [SerializeField] private Transform _presetContainer;

    [Tooltip("프리셋 줄 전체(제목 포함). 카탈로그에 프리셋이 없으면 줄째 접는다 - "
        + "버튼만 없으면 빈 줄이 남는다.")]
    [WiringOptional]
    [SerializeField] private GameObject _presetRow;

    [Header("하단")]
    [Tooltip("난이도 점수 합계. 이 모드의 진행 지표라 가장 크게 보여준다.")]
    [SerializeField] private TMP_Text _totalScoreText;

    [Tooltip("클리어한 최고 난이도 점수(MetaProgress).")]
    [SerializeField] private TMP_Text _bestScoreText;

    [Tooltip("상호배타 거부·프리셋 실패 사유를 띄우는 줄.")]
    [SerializeField] private TMP_Text _statusText;

    [Header("버튼")]
    [SerializeField] private Button _startButton;

    [Tooltip("우상단 닫기 버튼.")]
    [SerializeField] private Button _closeButton;

    [Tooltip("창 바깥(전면 dim) 클릭으로 닫기. 보통 이 창 루트의 Button.")]
    [SerializeField] private Button _blockerButton;

    [Tooltip("창을 닫는 키 - 보통 Esc.")]
    [SerializeField] private InputActionReference _closeAction;

    [Header("라벨")]
    [SerializeField] private LocalizedText _headerLabel;
    [SerializeField] private LocalizedText _presetHeaderLabel;
    [SerializeField] private LocalizedText _startLabel;
    [SerializeField] private LocalizedText _closeLabel;

    private ComponentPool<UI_MutatorSlot> _slotPool;

    private ComponentPool<UI_MutatorPresetButton> _presetPool;

    private NewGamePlusSelection _selection;

    // UI_TitleWindow가 Construct로 넣어 준다(UI_LoadGameWindow·UI_TutorialPromptPanel과 같은 주입 방식).
    private string _gameSceneName;

    // 마지막 조작의 실패 사유. null이면 상태 줄이 비어 있다(UI_LoadGameWindow.RenderStatus와 같은 처리).
    private string _statusLocKey;

    // 씬 로드는 되돌릴 수 없으므로 두 번 눌려도 한 번만 나간다.
    private bool _hasRequested;

    private bool _isOpen;

    private void Awake()
    {
        WiringGuard.Require(_catalog, nameof(_catalog), this);

        _selection = new NewGamePlusSelection(_catalog);

        _slotPool = new ComponentPool<UI_MutatorSlot>(_slotPrefab, _slotContainer);
        _presetPool = new ComponentPool<UI_MutatorPresetButton>(_presetButtonPrefab, _presetContainer);

        if (_startButton != null)
        {
            _startButton.onClick.AddListener(StartRun);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.AddListener(Close);
        }

        ApplyLabelKeys();

        // 창이 씬에 비활성으로 저장돼 있으므로, 이 Awake는 첫 Open()의 SetActive(true) 안에서
        // 동기 실행된다. Open()이 SetActive보다 먼저 _isOpen을 세우기 때문에 여기서 자기를 닫지 않는다 -
        // 이 가드를 빼면 첫 클릭이 스스로를 닫아 두 번째부터 열린다(CLAUDE.md의 _isOpen 가드).
        // 아래 분기는 누가 창을 활성으로 저장했을 때만 도는 안전망이다.
        if (!_isOpen)
        {
            CloseSilently();
        }
    }

    private void OnDestroy()
    {
        if (_startButton != null)
        {
            _startButton.onClick.RemoveListener(StartRun);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        if (_blockerButton != null)
        {
            _blockerButton.onClick.RemoveListener(Close);
        }
    }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += Render;

        if (_closeAction != null)
        {
            // 액션을 켜는 것은 GlobalInputBootstrap의 몫이다(UI_LoadGameWindow와 같은 판단).
            _closeAction.action.performed += OnCloseActionPerformed;
        }

        // 구독 직후 현재 값을 한 번 반영한다(CLAUDE.md 이벤트 초기화 규칙).
        Render();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= Render;

        if (_closeAction != null)
        {
            _closeAction.action.performed -= OnCloseActionPerformed;
        }

        // 지난번 실패 사유가 다음에 열 때까지 남아 있으면 안 된다.
        _statusLocKey = null;
    }

    /// <summary>시작 버튼으로 열 씬을 지정한다. 타이틀 화면이 한 번 호출한다.</summary>
    public void Construct(string gameSceneName)
    {
        _gameSceneName = gameSceneName;
    }

    public void Open()
    {
        SoundManager.Play(SoundId.UiWindowOpen);

        _isOpen = true;              // SetActive 이전에 세운다
        gameObject.SetActive(true);  // 첫 활성화라면 이 안에서 Awake가 돈다
    }

    public void Close()
    {
        // 씬 로드를 이미 요청했으면 닫지 않는다(UI_TutorialPromptPanel.Close와 같은 방어).
        if (_hasRequested)
        {
            return;
        }

        SoundManager.Play(SoundId.UiWindowClose);
        CloseSilently();
    }

    // Awake의 안전망 닫기가 창 닫는 소리를 내지 않게 소리 없는 경로를 따로 둔다.
    private void CloseSilently()
    {
        _isOpen = false;
        gameObject.SetActive(false);
    }

    private void OnCloseActionPerformed(InputAction.CallbackContext context)
    {
        Close();
    }

    // 라벨 key를 인스펙터가 아니라 코드에서 넣는다 - 프리팹에만 있는 key는 상수와 조용히 어긋난다
    // (UI_TutorialPromptPanel.ApplyLabelKeys와 같은 처리).
    private void ApplyLabelKeys()
    {
        if (_headerLabel != null)
        {
            _headerLabel.SetKey(NewGamePlusLocKeys.WINDOW_HEADER);
        }

        if (_presetHeaderLabel != null)
        {
            _presetHeaderLabel.SetKey(NewGamePlusLocKeys.PRESET_HEADER);
        }

        if (_startLabel != null)
        {
            _startLabel.SetKey(NewGamePlusLocKeys.START);
        }

        if (_closeLabel != null)
        {
            _closeLabel.SetKey(NewGamePlusLocKeys.CLOSE);
        }
    }

    private void Render()
    {
        if (_selection == null || _slotPool == null)
        {
            return;
        }

        RenderSlots();
        RenderPresets();
        RenderFooter();
    }

    private void RenderSlots()
    {
        int count = _selection.Count;

        for (int i = 0; i < count; i++)
        {
            _slotPool.Get(i).Bind(_selection, i, HandleTierPicked);
        }

        _slotPool.DeactivateFrom(count);

        RebuildLayout(_slotContainer);
    }

    // 프리셋 개수도 데이터에서 나온다 - 버튼 수를 코드나 프리팹에 박지 않는다.
    private void RenderPresets()
    {
        RunMutatorCatalogSO.Preset[] presets = Presets;
        int count = 0;

        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i] == null)
            {
                continue;
            }

            _presetPool.Get(count++).Setup(
                i,
                presets[i].NameLocKey,
                _selection.MatchesPreset(presets[i]),
                HandlePresetPicked);
        }

        _presetPool.DeactivateFrom(count);

        // 버튼만 없으면 레이아웃에 빈 줄이 남는다. 줄을 끄면 자리째 거둔다
        // (UI_ConfigWindow.RenderSlotButtons와 같은 처리).
        if (_presetRow != null)
        {
            _presetRow.SetActive(count > 0);
        }

        RebuildLayout(_presetContainer);
    }

    private void RenderFooter()
    {
        if (_totalScoreText != null)
        {
            _totalScoreText.text = string.Format(
                StringTable.GetString(NewGamePlusLocKeys.TOTAL_SCORE), _selection.DifficultyScore);
        }

        if (_bestScoreText != null)
        {
            _bestScoreText.text = string.Format(
                StringTable.GetString(NewGamePlusLocKeys.BEST_SCORE), MetaProgress.BestDifficultyScore);
        }

        if (_statusText != null)
        {
            _statusText.text = _statusLocKey == null
                ? string.Empty
                : StringTable.GetString(_statusLocKey);
        }
    }

    private RunMutatorCatalogSO.Preset[] Presets =>
        _catalog != null ? _catalog.Presets : Array.Empty<RunMutatorCatalogSO.Preset>();

    private static void RebuildLayout(Transform container)
    {
        if (container is RectTransform rect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }

    // 판정은 NewGamePlusSelection이 한다 - 상호배타 규칙을 UI에서 재구현하지 않는다
    // (같은 규칙을 세이브 복원 검사와 나눠 쓰기 때문에, 두 벌이 되면 조용히 갈라진다).
    private void HandleTierPicked(int index, int tier)
    {
        if (!_selection.TrySetTier(index, tier))
        {
            SoundManager.Play(SoundId.BuildBlocked);
            _statusLocKey = NewGamePlusLocKeys.STATUS_EXCLUSIVE_CONFLICT;
            Render();
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);
        _statusLocKey = null;
        Render();
    }

    private void HandlePresetPicked(int presetIndex)
    {
        RunMutatorCatalogSO.Preset[] presets = Presets;

        if (presetIndex < 0 || presetIndex >= presets.Length)
        {
            return;
        }

        if (!_selection.TryApplyPreset(presets[presetIndex]))
        {
            SoundManager.Play(SoundId.BuildBlocked);
            _statusLocKey = NewGamePlusLocKeys.STATUS_PRESET_INVALID;
            Render();
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);
        _statusLocKey = null;
        Render();
    }

    private void StartRun()
    {
        if (_hasRequested)
        {
            return;
        }

        if (string.IsNullOrEmpty(_gameSceneName))
        {
            Debug.LogError("[UI_NewGamePlusWindow] 시작할 씬이 지정되지 않았습니다. Construct 호출을 확인하세요.", this);
            return;
        }

        _hasRequested = true;
        SoundManager.Play(SoundId.UiButtonClick);

        Debug.Log($"[UI_NewGamePlusWindow] 새 게임 + 시작 - 뮤테이터 {_selection.SelectedCount}종,"
            + $" 난이도 점수 {_selection.DifficultyScore}");

        NewGamePlusRequest.Request(_selection.BuildRequest());
        SceneManager.LoadScene(_gameSceneName);
    }
}
