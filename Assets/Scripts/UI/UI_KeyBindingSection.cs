using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 설정 창 우측 키 섹션. 카탈로그에 적힌 액션들의 <b>키보드 바인딩을 훑어 줄을 런타임에 만들고</b>,
/// 줄을 누르면 리바인딩을 수행한다. 액션에 키를 하나 더 붙이면 이 스크립트를 고치지 않아도 줄이 는다.
/// 저장·복원은 SettingsService가 맡는다 - 이 스크립트는 표시와 입력만 맡는다(UI_ConfigWindow와 같은 역할 분담).
/// </summary>
public class UI_KeyBindingSection : MonoBehaviour
{
    // 이 접두사로 시작하는 바인딩만 줄로 만든다 - 마우스·게임패드·조이스틱·XR는 자동으로 걸러진다.
    private const string KEYBOARD_PATH_PREFIX = "<Keyboard>/";

    // 리바인딩으로 받아들일 장치. 표시가 키보드 바인딩만 훑으므로(KEYBOARD_PATH_PREFIX) 캡처도 같은 범위로
    // 묶어야 한다 - 게임패드 버튼이 들어가 버리면 그 줄이 목록에서 통째로 사라져 되돌릴 길이 없어진다.
    private const string KEYBOARD_CONTROL_PATH = "<Keyboard>";

    // 리바인딩 중 무시할 장치. 마우스로 창을 조작하다 키가 바뀌는 사고를 막는다.
    private const string MOUSE_CONTROL_PATH = "<Mouse>";

    // 리바인딩을 취소하는 키.
    private const string CANCEL_CONTROL_PATH = "<Keyboard>/escape";

    // 한 줄이 담당하는 바인딩. 표시와 충돌 검사가 같은 목록을 본다.
    private readonly struct RowData
    {
        public readonly InputAction Action;
        public readonly int BindingIndex;
        public readonly string Label;

        public RowData(InputAction action, int bindingIndex, string label)
        {
            Action = action;
            BindingIndex = bindingIndex;
            Label = label;
        }
    }

    [Header("Dependencies")]
    [Tooltip("설정창에 노출할 액션 목록.")]
    [SerializeField] private KeyBindingCatalog _catalog;

    [Header("배치")]
    [Tooltip("키 한 줄 프리팹(Slot_KeyBindingRow).")]
    [SerializeField] private UI_KeyBindingRow _rowPrefab;

    [Tooltip("생성된 줄이 들어갈 부모(스크롤 뷰의 Content).")]
    [SerializeField] private Transform _rowContainer;

    [Tooltip("모든 키를 기본값으로 되돌리는 버튼.")]
    [SerializeField] private Button _resetButton;

    [Header("입력")]
    [Tooltip("창을 닫는 액션(Esc). 리바인딩 중에는 Esc가 창까지 닫지 않도록 잠시 꺼 둔다.")]
    [SerializeField] private InputActionReference _closeAction;

    // 화면에 그리는 줄.
    private readonly List<RowData> _rowData = new();

    // 중복 키 검사 대상. 줄로 노출하지 않는 바인딩(WASD와 겹치는 화살표키)도 실제로 눌리는 키라 여기엔 들어가고,
    // 반대로 공유가 허용된 보정키는 여기서 빠진다 - 그래서 표시 목록과 따로 관리한다.
    private readonly List<RowData> _conflictData = new();

    // 세이브·볼륨 줄과 같은 방식으로 창이 주입한다 - 씬 참조라 프리팹 안에서는 배선할 수 없다.
    private SettingsService _settings;

    private ComponentPool<UI_KeyBindingRow> _rowPool;
    private int _constructedRowCount;

    private InputActionRebindingExtensions.RebindingOperation _activeRebind;
    private UI_KeyBindingRow _rebindingRow;
    private bool _wasRebindingActionEnabled;

    // 리바인딩을 시작하기 전에 걸려 있던 오버라이드. 충돌로 거부할 때 여기로 되돌린다.
    private string _rebindingPreviousOverridePath;

    // 프리팹·컨테이너가 비어 있으면 줄을 아예 만들 수 없다 - 조용히 빈 섹션이 되지 않게 여기서 잡는다.
    private bool HasRowFactory =>
        WiringGuard.Require(_rowPrefab, nameof(_rowPrefab), this) &&
        WiringGuard.Require(_rowContainer, nameof(_rowContainer), this);

    private void Awake()
    {
        if (_resetButton != null)
        {
            _resetButton.onClick.AddListener(ResetAll);
        }
    }

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += Render;
        SubscribeSettings();

        // 구독 직후 현재 값을 한 번 반영해 초기 발화를 놓쳐도 안전하게 한다.
        Render();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= Render;
        UnsubscribeSettings();

        // 창이 닫히는데 리바인딩이 떠 있으면 입력이 먹통이 된 것처럼 보인다.
        CancelActiveRebind();
    }

    /// <summary>창(UI_ConfigWindow)이 Awake에서 한 번 호출해 설정 서비스를 넣어준다.
    /// 창의 Awake와 이 컴포넌트의 OnEnable은 순서가 보장되지 않으므로, 어느 쪽이 먼저 와도
    /// 구독과 첫 표시가 한 번씩 이뤄지도록 양쪽에서 처리한다.</summary>
    public void Construct(SettingsService settings)
    {
        UnsubscribeSettings();
        _settings = settings;

        if (isActiveAndEnabled)
        {
            SubscribeSettings();
            Render();
        }
    }

    private void SubscribeSettings()
    {
        if (_settings == null)
        {
            return;
        }

        // 창 Awake와 OnEnable 양쪽에서 들어올 수 있어 중복 구독을 먼저 걷어낸다.
        _settings.OnKeyBindingsChanged -= Render;
        _settings.OnKeyBindingsChanged += Render;
    }

    private void UnsubscribeSettings()
    {
        if (_settings != null)
        {
            _settings.OnKeyBindingsChanged -= Render;
        }
    }

    /// <summary>카탈로그를 훑어 줄을 다시 만든다.</summary>
    public void Render()
    {
        // 키를 기다리는 동안에는 다시 그리지 않는다 - 줄을 갈아끼우면 안내 문구가 지워져 대기가 끝난 것처럼
        // 보이는데 캡처는 그대로 살아 있어, 사용자가 무심코 누른 다음 키가 그 바인딩에 들어가 버린다.
        // (언어 전환처럼 리바인딩 중에도 눌릴 수 있는 버튼이 Render를 부른다. 끝나면 FinishRebind가 다시 그린다.)
        if (_activeRebind != null)
        {
            return;
        }

        if (!HasRowFactory)
        {
            return;
        }

        // 창의 Render가 이 컴포넌트의 Awake보다 먼저 올 수 있어 여기서 준비한다.
        _rowPool ??= new ComponentPool<UI_KeyBindingRow>(_rowPrefab, _rowContainer);

        RebuildRowData();

        for (int i = 0; i < _rowData.Count; i++)
        {
            UI_KeyBindingRow row = _rowPool.Get(i);

            // 풀이 새로 만든 줄만 의존성을 넣는다(Get이 순서대로 늘어나므로 인덱스로 판별된다).
            if (i >= _constructedRowCount)
            {
                row.Construct(this);
                _constructedRowCount = i + 1;
            }

            RowData data = _rowData[i];
            row.Setup(data.Action, data.BindingIndex, data.Label);
        }

        _rowPool.DeactivateFrom(_rowData.Count);

        if (_rowContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    /// <summary>줄에서 키 버튼을 눌렀을 때 호출된다.</summary>
    public void BeginRebind(UI_KeyBindingRow row, InputAction action, int bindingIndex)
    {
        // 한 번에 하나만 - 다른 줄이 대기 중이면 무시한다.
        if (_activeRebind != null)
        {
            return;
        }

        _rebindingRow = row;
        _wasRebindingActionEnabled = action.enabled;
        _rebindingPreviousOverridePath = action.bindings[bindingIndex].overridePath;

        // PerformInteractiveRebinding은 켜져 있는 액션에 걸 수 없다.
        action.Disable();

        // Esc는 리바인딩 취소로만 쓰이게 하고, 설정창까지 닫히지 않도록 닫기 액션을 잠시 끈다.
        if (_closeAction != null)
        {
            _closeAction.action.Disable();
        }

        row.ShowWaiting();

        _activeRebind = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsHavingToMatchPath(KEYBOARD_CONTROL_PATH)
            .WithControlsExcluding(MOUSE_CONTROL_PATH)
            .WithCancelingThrough(CANCEL_CONTROL_PATH)
            .OnCancel(_ => FinishRebind(action, bindingIndex, false))
            .OnComplete(_ => FinishRebind(action, bindingIndex, true))
            .Start();
    }

    private void FinishRebind(InputAction action, int bindingIndex, bool isApplied)
    {
        DisposeActiveRebind();

        string conflictingLabel = null;
        bool hasConflict = isApplied && TryFindConflict(action, bindingIndex, out conflictingLabel);
        if (hasConflict)
        {
            // 거부 - 리바인딩 직전 상태로 되돌린다. RemoveBindingOverride는 오버라이드를 통째로 벗겨
            // 에셋 기본값으로 보내므로, 사용자가 전에 저장해 둔 키가 있었다면 그것을 다시 얹어야 한다.
            // (되돌린 결과가 저장된 값과 같으므로 여기서는 저장하지 않는다.)
            if (string.IsNullOrEmpty(_rebindingPreviousOverridePath))
            {
                action.RemoveBindingOverride(bindingIndex);
            }
            else
            {
                action.ApplyBindingOverride(bindingIndex, _rebindingPreviousOverridePath);
            }
        }
        else if (isApplied && _settings != null)
        {
            _settings.SaveKeyBindings();
        }

        _rebindingPreviousOverridePath = null;

        if (_wasRebindingActionEnabled)
        {
            action.Enable();
        }

        if (_closeAction != null)
        {
            _closeAction.action.Enable();
        }

        // Render가 모든 줄의 문구를 지우므로, 경고는 다시 그린 뒤에 띄운다.
        UI_KeyBindingRow row = _rebindingRow;
        _rebindingRow = null;
        Render();

        if (hasConflict && row != null)
        {
            row.ShowConflict(conflictingLabel);
        }
        else if (row != null)
        {
            row.ClearMessage();
        }
    }

    private void ResetAll()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        // 되돌리는 중에 대기 중인 리바인딩이 있으면 먼저 접는다.
        CancelActiveRebind();

        if (_settings != null)
        {
            _settings.ResetKeyBindings();
        }

        Render();
    }

    private void CancelActiveRebind()
    {
        // Cancel()이 OnCancel 콜백을 불러 FinishRebind가 뒷정리(액션 재활성화 등)를 하게 한다.
        _activeRebind?.Cancel();
        DisposeActiveRebind();
    }

    private void DisposeActiveRebind()
    {
        if (_activeRebind == null)
        {
            return;
        }

        _activeRebind.Dispose();
        _activeRebind = null;
    }

    // 카탈로그의 액션들을 훑어 "키보드 바인딩 한 개 = 한 줄"로 펼친다.
    private void RebuildRowData()
    {
        _rowData.Clear();
        _conflictData.Clear();

        if (_catalog == null)
        {
            return;
        }

        foreach (KeyBindingCatalog.Entry entry in _catalog.Entries)
        {
            if (entry.Action == null)
            {
                continue;
            }

            AddRowsForAction(entry.Action.action, entry.DisplayLocKey, entry.AllowsSharedKey);
        }
    }

    private void AddRowsForAction(InputAction action, string displayLocKey, bool allowsSharedKey)
    {
        if (action == null)
        {
            return;
        }

        string labelFormat = StringTable.GetString(displayLocKey);

        // 같은 방향에 키가 둘 이상 걸린 경우(WASD와 화살표) 줄은 첫 번째만 노출한다.
        var shownParts = new HashSet<string>();
        int slot = 0;

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            // 합성 바인딩의 머리(Dpad 등)는 키가 아니라 묶음이라 줄로 만들지 않는다.
            if (binding.isComposite || !IsKeyboardBinding(binding))
            {
                continue;
            }

            var data = new RowData(action, i, FormatLabel(labelFormat, binding, slot));

            // 검사 목록에는 숨길 바인딩까지 넣는다 - 화살표키는 줄로 보이지 않을 뿐 실제로 눌리는 키라,
            // 빼 두면 다른 액션이 화살표키를 가져가도 충돌로 잡히지 않고 두 동작이 함께 발동한다.
            if (!allowsSharedKey)
            {
                _conflictData.Add(data);
            }

            if (binding.isPartOfComposite && !shownParts.Add(binding.name))
            {
                continue;
            }

            _rowData.Add(data);
            slot++;
        }
    }

    // 라벨의 {0}에는 합성 바인딩이면 방향 이름이, 여러 키를 가진 액션이면 번호가 들어간다.
    // {0}이 없는 라벨(키가 하나뿐인 액션)은 인자를 그냥 무시한다.
    private static string FormatLabel(string labelFormat, InputBinding binding, int slot)
    {
        if (binding.isPartOfComposite)
        {
            string partLocKey = KeyBindingLocKeys.PartLocKey(binding.name);
            string partName = partLocKey == null ? binding.name : StringTable.GetString(partLocKey);
            return string.Format(labelFormat, partName);
        }

        return string.Format(labelFormat, slot + 1);
    }

    private static bool IsKeyboardBinding(InputBinding binding)
    {
        return binding.effectivePath != null &&
               binding.effectivePath.StartsWith(KEYBOARD_PATH_PREFIX, StringComparison.Ordinal);
    }

    // 방금 바뀐 키가 카탈로그의 다른 바인딩에서 이미 쓰이고 있는지 본다.
    // 카탈로그에 없는 액션(마우스 전용 등)과 공유가 허용된 보정키는 보지 않는다.
    private bool TryFindConflict(InputAction action, int bindingIndex, out string conflictingLabel)
    {
        conflictingLabel = null;

        // 바뀐 쪽이 공유를 허용한 바인딩이면 어디와 겹치든 따지지 않는다 - 검사 목록에서 빠져 있는 것이 그 표시다.
        if (!IsConflictChecked(action, bindingIndex))
        {
            return false;
        }

        string newPath = action.bindings[bindingIndex].effectivePath;

        foreach (RowData data in _conflictData)
        {
            if (data.Action == action && data.BindingIndex == bindingIndex)
            {
                continue;
            }

            if (data.Action.bindings[data.BindingIndex].effectivePath == newPath)
            {
                conflictingLabel = data.Label;
                return true;
            }
        }

        return false;
    }

    private bool IsConflictChecked(InputAction action, int bindingIndex)
    {
        foreach (RowData data in _conflictData)
        {
            if (data.Action == action && data.BindingIndex == bindingIndex)
            {
                return true;
            }
        }

        return false;
    }
}
