using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 되돌릴 수 없는 조작 직전에 한 번 물어보는 범용 YES/NO 확인창.
///
/// 문구와 확인 콜백만 호출부가 넘기므로 상황마다 클래스를 새로 만들지 않는다 -
/// 같은 성격의 경고를 붙일 때 <see cref="Open(string, Action, object[])"/> 한 줄이면 된다
/// (UI_SlotConfirmPopup이 슬롯 번호에 묶여 있어 재사용할 수 없었다).
///
/// 문구는 여러 줄을 받는다. 동시에 걸린 경고를 창 여러 개로 쪼개 연달아 묻지 않고
/// 한 창에 모아 보여주기 위함이다(예: 밤 진입 전 굶주림 + 유휴 인구).
///
/// 문구가 런타임 값({0} 등)을 포함하므로 <see cref="LocalizedText"/>를 쓰지 않고
/// 이 컴포넌트가 직접 채운다. 대신 언어 전환에는 스스로 반응한다.
/// </summary>
public class UI_ConfirmPopup : MonoBehaviour
{
    // 여러 줄을 이어 붙일 때 쓰는 구분자.
    private const string MESSAGE_LINE_SEPARATOR = "\n";

    [Tooltip("확인 문구를 그릴 텍스트. 코드가 채우므로 LocalizedText는 붙이지 않는다.")]
    [SerializeField] private TMP_Text _messageText;

    [Tooltip("확정 버튼(YES). 누르면 팝업을 닫고 콜백을 실행한다.")]
    [SerializeField] private Button _yesButton;

    [Tooltip("아무것도 하지 않고 팝업만 닫는 버튼(NO).")]
    [SerializeField] private Button _noButton;

    [Tooltip("문구가 길어진 만큼 세로로 늘릴 상자. 비워두면 늘리지 않는다 - " +
        "그때는 인스펙터에 잡아 둔 높이를 넘는 문구가 버튼 위로 넘쳐 흐른다.")]
    [WiringOptional]
    [SerializeField] private RectTransform _box;

    // 상자에서 문구 칸을 뺀 나머지 높이(위 여백 + 문구·버튼 간격 + 버튼 + 아래 여백).
    // 인스펙터에 잡아 둔 두 높이의 차이가 그대로 이 값이라, 여백을 코드에 상수로 적지 않아도 된다.
    private float _boxChromeHeight;
    private bool _canFitToMessage;

    // 호출부가 넘긴 줄을 그대로 참조하지 않고 여기에 복사해 둔다 - 호출부가 버퍼를 재사용해도
    // 떠 있는 동안(언어 전환 시 다시 그릴 때) 문구가 바뀌지 않아야 한다.
    private readonly List<ConfirmMessageLine> _messageLines = new();

    // 줄을 이어 붙일 때 쓰는 재사용 버퍼. 언어를 바꿀 때마다 다시 만들지 않기 위함.
    private readonly StringBuilder _messageBuilder = new();

    private Action _confirmed;
    private bool _isOpen;

    /// <summary>팝업이 떠 있는지. _isOpen이 아니라 실제 활성 상태를 본다 -
    /// 부모가 꺼지면서 같이 사라질 때는 Close()를 거치지 않는다
    /// (UI_SlotConfirmPopup.IsOpen과 같은 이유).</summary>
    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        if (_yesButton != null)
        {
            _yesButton.onClick.AddListener(HandleYes);
        }

        if (_noButton != null)
        {
            _noButton.onClick.AddListener(HandleNo);
        }

        // 문구를 처음 채우기 전에 재야 한다 - 한 번이라도 늘어난 뒤에 읽으면 늘어난 높이가 기준이 된다.
        // (첫 Open()이 유발한 Awake라도 문구를 그리는 OnEnable보다 먼저 돈다.)
        if (_messageText != null && _box != null)
        {
            _boxChromeHeight = _box.sizeDelta.y - _messageText.rectTransform.sizeDelta.y;
            _canFitToMessage = true;
        }

        // 프리팹 인스턴스가 활성으로 저장돼 있어도 시작 시 닫힌 상태를 보장한다.
        //
        // _isOpen 가드가 필요한 이유: 인스턴스가 비활성으로 저장된 경우 Unity는 부모가 켜질 때
        // Awake를 호출하지 않고, 첫 Open()의 SetActive(true) 안에서 비로소 이 Awake가 동기 실행된다.
        // 그때 무조건 닫으면 방금 연 팝업을 스스로 닫아 첫 클릭이 먹지 않는다.
        // Open()은 SetActive(true) 전에 _isOpen을 세우므로, 이 값으로 두 경우를 구분한다
        // (CLAUDE.md 이벤트 초기화 규칙).
        if (!_isOpen)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_yesButton != null)
        {
            _yesButton.onClick.RemoveListener(HandleYes);
        }

        if (_noButton != null)
        {
            _noButton.onClick.RemoveListener(HandleNo);
        }
    }

    // 구독은 떠 있는 동안만 유효하면 된다 - 닫히면 오브젝트가 꺼지므로 OnEnable/OnDisable이
    // 그대로 열림 구간이 된다. 구독 직후 현재 값을 한 번 반영해 초기 발화를 놓쳐도 안전하게 한다
    // (LocalizedText와 같은 형태).
    private void OnEnable()
    {
        StringTable.OnLanguageChanged += ApplyMessage;
        ApplyMessage();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= ApplyMessage;
    }

    /// <summary>
    /// 한 줄짜리 확인을 띄운다. YES를 누르면 <paramref name="onConfirmed"/>가 실행되고, NO를 누르면 아무 일도 없다.
    /// </summary>
    /// <param name="messageLocKey">스트링테이블 key. 값에 {0}, {1}이 있으면 messageArgs로 채운다.</param>
    /// <param name="messageArgs">문구에 끼워 넣을 값들. 없으면 key의 문구를 그대로 쓴다.</param>
    public void Open(string messageLocKey, Action onConfirmed, params object[] messageArgs)
    {
        _messageLines.Clear();
        _messageLines.Add(new ConfirmMessageLine(messageLocKey, messageArgs));

        OpenWithCurrentLines(onConfirmed);
    }

    /// <summary>
    /// 여러 줄짜리 확인을 띄운다. 줄은 넘긴 순서대로 위에서 아래로 이어 붙는다.
    /// </summary>
    /// <param name="lines">문구 줄들. 이 목록의 내용은 복사되므로 호출부가 버퍼를 재사용해도 된다.</param>
    public void Open(Action onConfirmed, IReadOnlyList<ConfirmMessageLine> lines)
    {
        _messageLines.Clear();

        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i += 1)
            {
                _messageLines.Add(lines[i]);
            }
        }

        OpenWithCurrentLines(onConfirmed);
    }

    private void OpenWithCurrentLines(Action onConfirmed)
    {
        _confirmed = onConfirmed;

        // SetActive 이전에 세운다 - 첫 활성화라면 이 안에서 Awake가 돌고, 그 Awake가 이 값을 본다.
        _isOpen = true;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        _isOpen = false;

        // 확인 대상을 팝업 밖으로 들고 나가지 않는다 - 다음에 열 때 직전 요청이 처리되면 안 된다.
        _messageLines.Clear();
        _confirmed = null;

        gameObject.SetActive(false);
    }

    private void ApplyMessage()
    {
        if (_messageText == null || _messageLines.Count == 0)
        {
            return;
        }

        _messageBuilder.Clear();

        foreach (ConfirmMessageLine line in _messageLines)
        {
            if (string.IsNullOrEmpty(line.LocKey))
            {
                continue;
            }

            if (_messageBuilder.Length > 0)
            {
                _messageBuilder.Append(MESSAGE_LINE_SEPARATOR);
            }

            string message = StringTable.GetString(line.LocKey);

            _messageBuilder.Append(line.Args == null || line.Args.Length == 0
                ? message
                : string.Format(message, line.Args));
        }

        _messageText.text = _messageBuilder.ToString();

        FitToMessage();
    }

    // 문구 칸과 상자의 높이를 문구 줄 수에 맞춘다. 줄이 늘어도 버튼 위로 겹쳐 그려지지 않고,
    // 짧은 문구에서는 상자가 도로 줄어 빈 칸이 남지 않는다.
    //
    // 상자는 가운데 정렬이고 문구는 위, 버튼은 아래에 붙어 있어, 상자 높이만 바꾸면
    // 문구는 위 여백을, 버튼은 아래 여백을 그대로 유지한 채 둘 사이만 벌어진다.
    // (문구 칸의 세로 정렬은 Top이어야 한다 - 그래야 글자가 칸 위쪽부터 채워져 이 계산과 맞는다.)
    private void FitToMessage()
    {
        if (!_canFitToMessage)
        {
            return;
        }

        RectTransform messageRect = _messageText.rectTransform;

        // 폭은 상자에 맞춰 늘어나 있으므로, 그 폭으로 줄바꿈했을 때의 높이를 묻는다.
        float messageHeight = _messageText.GetPreferredValues(
            _messageText.text, messageRect.rect.width, 0f).y;

        messageRect.sizeDelta = new Vector2(messageRect.sizeDelta.x, messageHeight);
        _box.sizeDelta = new Vector2(_box.sizeDelta.x, _boxChromeHeight + messageHeight);
    }

    private void HandleYes()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        // 콜백이 화면을 다시 그리는 동안 팝업이 떠 있으면 안 되므로 먼저 닫는다.
        // Close()가 콜백을 비우므로, 호출에 쓸 값은 그 전에 지역 변수로 옮겨 둔다
        // (UI_SlotConfirmPopup.HandleYes와 같은 순서).
        Action confirmed = _confirmed;

        Close();

        confirmed?.Invoke();
    }

    private void HandleNo()
    {
        SoundManager.Play(SoundId.UiButtonClick);
        Close();
    }
}
