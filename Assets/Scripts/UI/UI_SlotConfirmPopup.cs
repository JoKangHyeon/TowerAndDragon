using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세이브 슬롯 하나에 대한 YES/NO 확인 팝업. 슬롯 목록 창(UI_LoadGameWindow)의 자식으로 붙는다.
/// 삭제 확인(Popup_SaveDelete)과 덮어쓰기 확인(Popup_SaveChange)이 이 컴포넌트를 함께 쓴다 -
/// 둘의 차이는 문구(LocalizedText)와 창이 넘겨주는 콜백뿐이라 별도 클래스를 두지 않는다.
///
/// 어느 슬롯인지만 들고 있다가 YES를 눌렀을 때 그 번호를 창에 되돌려 준다 - 실제 삭제·저장도,
/// 그것이 가능한지 판정도 창의 몫이다(UI_SaveSlotItem과 같은 방향: 슬롯 UI는 자기 데이터를 모른다).
/// </summary>
public class UI_SlotConfirmPopup : MonoBehaviour
{
    [Tooltip("확정 버튼(YES).")]
    [SerializeField] private Button _yesButton;

    [Tooltip("아무것도 하지 않고 팝업만 닫는 버튼(NO).")]
    [SerializeField] private Button _noButton;

    private Action<int> _confirmed;
    private int _slotIndex = SavePaths.INVALID_SLOT_INDEX;
    private bool _isOpen;

    /// <summary>팝업이 떠 있는지. _isOpen이 아니라 실제 활성 상태를 본다 -
    /// 창 전체가 닫힐 때는 부모가 꺼지면서 이 팝업도 같이 사라지는데 Close()를 거치지 않는다
    /// (UI_DragonChangePopup.IsOpen과 같은 이유).</summary>
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

        // 프리팹 인스턴스가 활성으로 저장돼 있어도 시작 시 닫힌 상태를 보장한다.
        //
        // _isOpen 가드가 필요한 이유: 인스턴스가 비활성으로 저장된 경우 Unity는 부모가 켜질 때
        // Awake를 호출하지 않고, 첫 Open()의 SetActive(true) 안에서 비로소 이 Awake가 동기 실행된다.
        // 그때 무조건 닫으면 방금 연 팝업을 스스로 닫아 첫 클릭이 먹지 않는다.
        // Open()은 SetActive(true) 전에 _isOpen을 세우므로, 이 값으로 두 경우를 구분한다
        // (UI_DragonChangePopup.Awake와 같은 방어 - CLAUDE.md 이벤트 초기화 규칙).
        if (!_isOpen)
        {
            Close();
        }
    }

    /// <summary>슬롯 하나에 대한 확인을 띄운다. YES를 누르면 onConfirmed에 그 슬롯 번호가 온다.</summary>
    public void Open(int slotIndex, Action<int> onConfirmed)
    {
        _slotIndex = slotIndex;
        _confirmed = onConfirmed;

        _isOpen = true;
        gameObject.SetActive(true);
    }

    public void Close()
    {
        _isOpen = false;

        // 확인 대상을 팝업 밖으로 들고 나가지 않는다 - 다음에 열 때 직전 슬롯이 처리되면 안 된다.
        _slotIndex = SavePaths.INVALID_SLOT_INDEX;
        _confirmed = null;

        gameObject.SetActive(false);
    }

    private void HandleYes()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        // 콜백이 목록을 다시 그리는 동안 팝업이 떠 있으면 안 되므로 먼저 닫는다.
        // Close()가 대상을 비우므로, 호출에 쓸 값은 그 전에 지역 변수로 옮겨 둔다.
        Action<int> confirmed = _confirmed;
        int slotIndex = _slotIndex;

        Close();

        confirmed?.Invoke(slotIndex);
    }

    private void HandleNo()
    {
        SoundManager.Play(SoundId.UiButtonClick);
        Close();
    }
}
