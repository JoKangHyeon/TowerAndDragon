using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 설정 창 키 섹션의 한 줄(액션 이름 + 현재 키 버튼 + 안내 문구).
/// UI_KeyBindingSection이 풀에서 꺼내 Construct/Setup으로 채운다 - UI_VolumeRow와 같은 구조지만,
/// 줄 수가 액션·바인딩 수에 따라 달라지므로 프리팹에 미리 깔지 않고 런타임에 만든다.
/// </summary>
public class UI_KeyBindingRow : MonoBehaviour
{
    [Tooltip("액션 이름(예: 건설 모드).")]
    [SerializeField] private TMP_Text _label;

    [Tooltip("누르면 리바인딩이 시작되는 버튼.")]
    [SerializeField] private Button _keyButton;

    [Tooltip("현재 키 표기. 보통 _keyButton의 자식 텍스트.")]
    [SerializeField] private TMP_Text _keyLabel;

    [Tooltip("입력 대기·충돌 안내 문구. 평소에는 비어 있다.")]
    [SerializeField] private TMP_Text _messageText;

    [Tooltip("충돌 경고를 띄워 둘 시간(초).")]
    [SerializeField] private float _messageDuration = 2.5f;

    private UI_KeyBindingSection _owner;
    private InputAction _action;
    private int _bindingIndex;

    // 경고 문구를 자동으로 지우는 대기. 새 문구가 뜨면 이전 대기는 취소한다.
    private CancellationTokenSource _messageCts;

    /// <summary>섹션이 풀에서 처음 꺼낼 때 한 번 호출해 의존성과 입력을 연결한다.</summary>
    public void Construct(UI_KeyBindingSection owner)
    {
        _owner = owner;

        if (_keyButton != null)
        {
            _keyButton.onClick.AddListener(BeginRebind);
        }
    }

    /// <summary>이 줄이 담당할 바인딩과 표시 이름을 정한다. 갱신할 때마다 호출된다.</summary>
    public void Setup(InputAction action, int bindingIndex, string label)
    {
        _action = action;
        _bindingIndex = bindingIndex;

        if (_label != null)
        {
            _label.text = label;
        }

        ClearMessage();
        Render();
    }

    /// <summary>현재 바인딩의 키 표기를 다시 그린다.</summary>
    public void Render()
    {
        if (_keyLabel == null || _action == null)
        {
            return;
        }

        _keyLabel.text = _action.GetBindingDisplayString(_bindingIndex);
    }

    /// <summary>키 입력을 기다리는 중임을 보여준다. 문구는 리바인딩이 끝날 때까지 남는다.</summary>
    public void ShowWaiting()
    {
        CancelPendingMessage();

        if (_keyLabel != null)
        {
            _keyLabel.text = string.Empty;
        }

        SetMessage(StringTable.GetString(KeyBindingLocKeys.WAITING));
    }

    /// <summary>이미 쓰이는 키라 거부됐음을 알린다. 잠시 뒤 저절로 사라진다.</summary>
    public void ShowConflict(string conflictingActionLabel)
    {
        CancelPendingMessage();
        SetMessage(string.Format(
            StringTable.GetString(KeyBindingLocKeys.CONFLICT), conflictingActionLabel));

        _messageCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        HideMessageAfterDelayAsync(_messageCts.Token).Forget();
    }

    public void ClearMessage()
    {
        CancelPendingMessage();
        SetMessage(string.Empty);
    }

    private void OnDestroy()
    {
        CancelPendingMessage();
    }

    private void BeginRebind()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_owner != null && _action != null)
        {
            _owner.BeginRebind(this, _action, _bindingIndex);
        }
    }

    private async UniTaskVoid HideMessageAfterDelayAsync(CancellationToken token)
    {
        try
        {
            // 설정창은 일시정지(timeScale 0) 중에도 열리므로 스케일되지 않은 시간으로 센다.
            await UniTask.Delay(
                TimeSpan.FromSeconds(_messageDuration),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token);
        }
        catch (OperationCanceledException)
        {
            // 새 문구가 떴거나 줄이 파괴된 경우 - 지울 것이 없다.
            return;
        }

        SetMessage(string.Empty);
    }

    private void SetMessage(string message)
    {
        if (_messageText != null)
        {
            _messageText.text = message;
        }
    }

    private void CancelPendingMessage()
    {
        if (_messageCts == null)
        {
            return;
        }

        _messageCts.Cancel();
        _messageCts.Dispose();
        _messageCts = null;
    }
}
