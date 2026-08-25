using System;
using UnityEngine;
using UnityEngine.UI;

// 어미용 속성 변경 팝업(Popup_Dragon_Change). Dragon_window의 Button_change가 이 팝업을 연다.
// 카드 하나 = 속성 하나(Type_Ice/Fire/Time/Stone/Life) - 지금은 모든 카드가 같은 자리표시 스프라이트를
// 쓰고 있어 이미지로 속성을 구분할 수 없다. 그래서 버튼은 GetComponentsInChildren 같은 자동 탐색이 아니라
// 인스펙터에서 카드 이름을 보고 하나씩 배정한다(_attributeButtons 배열 - DragonType 선언 순서를 따른다).
// 실제 변경은 재구현하지 않고 Dragon.TryChangeType에 위임한다 -
// UI_MainCastleWindow.ApplyDragonType과 같은 호출 순서(TryChangeType → NotifyActiveAttributeChanged).
public class UI_DragonChangePopup : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("현재 어미용을 들고 있는 출처.")]
    [SerializeField] private GameManager _gameManager;
    [Tooltip("변경 성공 시 다른 UI(용 창 좌측 프레임 등)에 알리는 데 쓴다.")]
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    [Header("속성 선택 버튼 5개 - DragonType 선언 순서(Ice, Fire, Time, Stone, Life)와 일치해야 한다.")]
    [Tooltip("프리팹의 카드 배치 순서(Life, Fire, Ice, Time, Stone)와 다르므로, 자동 탐색 대신 " +
        "Type_Ice/Type_Fire/Type_Time/Type_Stone/Type_Life 카드 안의 Button을 이름 보고 하나씩 이 순서대로 배정한다.")]
    [SerializeField] private Button[] _attributeButtons;

    [Tooltip("속성 카드(Type_*) 5개 - 위 버튼 배열과 같은 순서. 현재 속성 카드를 숨기는 데 쓴다.")]
    [SerializeField] private GameObject[] _attributeCards;

    private static readonly DragonType[] ATTRIBUTES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    private bool _isOpen;

    /// <summary>팝업이 떠 있는지. Button_change가 열지 닫을지 판단하는 데 쓴다.
    /// _isOpen이 아니라 실제 활성 상태를 보는 이유: 용 창 전체가 닫힐 때는 부모(Content)가 꺼지면서
    /// 이 팝업도 같이 사라지는데 Close()를 거치지 않아 _isOpen이 true로 남는다.
    /// 그 값을 믿으면 다음에 Button_change를 눌렀을 때 "닫기"로 잘못 판정해 아무 일도 일어나지 않는다.</summary>
    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        if (_attributeButtons != null)
        {
            for (int i = 0; i < _attributeButtons.Length && i < ATTRIBUTES_IN_ORDER.Length; i++)
            {
                DragonType attribute = ATTRIBUTES_IN_ORDER[i];
                _attributeButtons[i]?.onClick.AddListener(() => SelectAttribute(attribute));
            }
        }

        // 프리팹 인스턴스가 활성으로 저장돼 있어도 시작 시 닫힌 상태를 보장한다.
        //
        // _isOpen 가드가 필요한 이유: 인스턴스가 비활성으로 저장된 경우 Unity는 부모가 켜질 때
        // Awake를 호출하지 않고, 첫 Open()의 SetActive(true) 안에서 비로소 이 Awake가 동기 실행된다.
        // 그때 무조건 닫으면 방금 연 팝업을 스스로 닫아 첫 클릭이 먹지 않는다.
        // Open()은 SetActive(true) 전에 _isOpen을 세우므로, 이 값으로 두 경우를 구분한다
        // (UI_DragonWindow.Awake와 같은 방어).
        if (!_isOpen)
        {
            Close();
        }
    }

    public void Open()
    {
        _isOpen = true;

        // 열 때마다 다시 계산한다 - 속성이 바뀌면 숨겨야 할 카드도 바뀐다.
        ApplyCardVisibility();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        _isOpen = false;
        gameObject.SetActive(false);
    }

    // 현재 속성 카드는 숨긴다 - 같은 속성으로 바꾸는 건 의미가 없다
    // (TryChangeType도 같은 속성이면 아무것도 하지 않는다).
    private void ApplyCardVisibility()
    {
        if (!WiringGuard.RequireNotEmpty(_attributeCards, nameof(_attributeCards), this))
        {
            return;
        }

        Dragon dragon = CurrentDragon;

        for (int i = 0; i < _attributeCards.Length && i < ATTRIBUTES_IN_ORDER.Length; i++)
        {
            if (_attributeCards[i] == null)
            {
                continue;
            }

            // 어미용 정보가 없으면 숨길 기준이 없으므로 전부 보여준다.
            bool isCurrentAttribute = dragon != null && dragon.CurrentType == ATTRIBUTES_IN_ORDER[i];
            _attributeCards[i].SetActive(!isCurrentAttribute);
        }
    }

    private Dragon CurrentDragon =>
        _gameManager != null ? _gameManager.CurrentRun?.CurrentDragon : null;

    /// <summary>속성을 바꿀 수 있는 시간대인지. 주기를 알 수 없으면 낮으로 본다
    /// (UI_DragonWindow.IsDay와 같은 fail-open 규약).</summary>
    private bool IsDay
    {
        get
        {
            CycleManager cycle = _gameManager != null ? _gameManager.CycleManager : null;
            return cycle == null || cycle.CurrentCycle == CycleManager.CycleState.Day;
        }
    }

    // 카드 클릭 = 그 속성으로 즉시 변경 + 팝업 닫기. 별도 확인 버튼은 없다(프리팹에 존재하지 않는다).
    // 실패하는 경우는 (1) 같은 속성을 고른 때 - 그 카드는 애초에 숨겨져 있다 - 와
    // (2) 굳은 맹세(sworn_element)로 이번 주기의 변경권을 다 쓴 때다. (2)는 UI_DragonWindow가
    // 팝업을 열기 전에 막고 사유를 표시하므로, 여기 도달하는 것은 다른 경로로 들어온 호출뿐이다.
    // 밤에는 UI_DragonWindow가 이 팝업을 열지도 않고 열려 있으면 닫지만, 실제 변경을 막는 최종 가드는 여기다 -
    // 다른 경로로 들어온 호출이나 밤이 시작된 프레임의 클릭까지 잡는다.
    private void SelectAttribute(DragonType attribute)
    {
        Dragon dragon = CurrentDragon;
        if (dragon == null || !IsDay)
        {
            Close();
            return;
        }

        // 안내가 막고 있으면 팝업이 열리지도 않지만(UI_DragonWindow.ToggleChangePopup), 열려 있는
        // 동안 단계가 지나갔거나 다른 경로로 들어온 호출까지 여기서 잡는다 - IsDay와 같은 최종 가드다.
        if (_dragonTreeManager != null && !_dragonTreeManager.CanChangeAttributeNow)
        {
            _dragonTreeManager.NotifyProgressionBlocked();
            Close();
            return;
        }

        // 굳은 맹세의 주기당 제한. 사유 표시는 팝업을 열기 전 UI_DragonWindow가 맡고
        // (이 팝업에는 문구를 띄울 요소가 없다), 여기서는 IsDay와 같은 최종 가드로만 쓴다 -
        // 다른 경로로 열린 팝업이나 열려 있는 동안 주기가 넘어간 경우까지 잡는다.
        CycleManager cycle = _gameManager != null ? _gameManager.CycleManager : null;
        RunModifierSnapshot snapshot = RunModifiers
            .SnapshotOf(_gameManager != null ? _gameManager.RunModifierService : null);

        bool changed = dragon.TryChangeType(
            attribute,
            DragonTypeChangeRules.ResolveCycleNumber(cycle),
            DragonTypeChangeRules.ResolveLimitPerCycle(snapshot),
            out DragonTypeChangeBlock _);

        // DragonTreeManager는 이 알림 없이는 속성 변경을 감지할 수 없다(Dragon.OnDragonTypeChanged가
        // 어디서도 invoke되지 않음) - 변경이 실제로 적용됐을 때만 알린다.
        if (changed && _dragonTreeManager != null)
        {
            _dragonTreeManager.NotifyActiveAttributeChanged();
        }

        Close();
    }
}
