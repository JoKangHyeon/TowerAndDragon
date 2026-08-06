using System;
using UnityEngine;
using UnityEngine.UI;

// 어미용 속성 변경 팝업(Popup_Dragon_Change). Dragon_window의 Button_change가 이 팝업을 연다.
// 카드 하나 = 속성 하나(Type_Ice/Fire/Time/Stone/Life) - 지금은 모든 카드가 같은 자리표시 스프라이트를
// 쓰고 있어 이미지로 속성을 구분할 수 없다. 그래서 버튼은 GetComponentsInChildren 같은 자동 탐색이 아니라
// 인스펙터에서 카드 이름을 보고 하나씩 배정한다(_attributeButtons 배열 - DragonType 선언 순서를 따른다).
// 실제 변경 규칙(TryChangeType의 낮 1회 제한)은 재구현하지 않고 Dragon에 위임한다 -
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

    // 현재 속성 카드는 숨긴다 - 같은 속성으로 바꾸는 건 의미가 없고,
    // TryChangeType이 성공 처리되면서 낮 1회 변경 기회만 소모된다.
    private void ApplyCardVisibility()
    {
        if (_attributeCards == null)
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

    // 카드 클릭 = 그 속성으로 즉시 변경 시도 + 팝업 닫기. 별도 확인 버튼은 없다(프리팹에 존재하지 않는다).
    // 변경이 낮 1회 제한에 막히면 TryChangeType이 조용히 false를 반환한다 - 이번 범위에서는
    // 실패 피드백 UI를 새로 만들지 않는다(요청에 없었고, MainCastleWindow도 실패 시 버튼을 비활성화할 뿐
    // 별도 안내 문구는 없다).
    private void SelectAttribute(DragonType attribute)
    {
        Dragon dragon = CurrentDragon;
        if (dragon == null)
        {
            Close();
            return;
        }

        bool changed = dragon.TryChangeType(attribute);

        // DragonTreeManager는 이 알림 없이는 속성 변경을 감지할 수 없다(Dragon.OnDragonTypeChanged가
        // 어디서도 invoke되지 않음) - 변경이 실제로 적용됐을 때만 알린다.
        if (changed && _dragonTreeManager != null)
        {
            _dragonTreeManager.NotifyActiveAttributeChanged();
        }

        Close();
    }
}
