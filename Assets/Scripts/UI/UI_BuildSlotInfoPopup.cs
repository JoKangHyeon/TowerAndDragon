using UnityEngine;

// 빌드모드 슬롯에 마우스를 올렸을 때 슬롯 오른쪽에 뜨는 정보 창들의 공통 뼈대
// (타워용 UI_TowerInfoPopup, 생산건물용 UI_FactoryInfoPopup).
// 뜨고 지는 시점은 스스로 정하지 않는다 - UI_BuildModeWindow가 호버를 받아 호출한다.
//
// 창을 띄울 때마다 내용을 새로 그리므로 언어 변경 구독은 두지 않는다.
public abstract class UI_BuildSlotInfoPopup : MonoBehaviour
{
    [Tooltip("슬롯 오른쪽 끝에서 팝업까지 띄울 간격(캔버스 단위).")]
    [SerializeField] private float _slotGap = 8f;

    // 슬롯의 네 모서리를 받는 재사용 버퍼 - 호버할 때마다 배열을 새로 만들지 않는다.
    private readonly Vector3[] _slotCorners = new Vector3[4];

    private RectTransform _rect;
    private bool _isShown;

    // Awake에서 캐시하지 않는다 - 이 팝업은 비활성으로 저장돼 있어 첫 Show의 SetActive 안에서야
    // Awake가 도는데, 그 타이밍에 의존하면 첫 호버에서만 위치를 못 잡고 원래 자리에 머무른다.
    private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

    // Awake에서 자기 자신을 닫지 않는다.
    //
    // 창·팝업들이 쓰는 "Awake에서 닫고 _isOpen 플래그로 첫 열기를 구분"하는 방어(CLAUDE.md)를 여기서는
    // 쓸 수 없다 - 실측(로그)상 첫 Show의 SetActive(true) 안에서 도는 Awake가 직전에 세운 플래그를
    // false로 읽어, 방금 연 팝업을 스스로 닫아버렸다(첫 호버만 안 뜨는 증상).
    // 닫힌 시작 상태는 UI_BuildModeWindow가 초기화 때 HideSlotInfo()로 보장하므로 여기서 할 일이 없다.
    public void Hide()
    {
        _isShown = false;
        gameObject.SetActive(false);
    }

    /// <summary>내용을 채운 파생 클래스가 마지막에 호출한다 - 슬롯 오른쪽에 띄운다.</summary>
    protected void ShowAt(RectTransform slotRect)
    {
        if (slotRect == null)
        {
            Hide();
            return;
        }

        _isShown = true;              // SetActive 이전에 세운다
        gameObject.SetActive(true);   // 첫 활성화라면 이 안에서 Awake가 돈다

        // 위치는 활성화 뒤에 잡는다 - 꺼져 있는 동안에는 레이아웃이 갱신되지 않아 크기가 어긋난다.
        PlaceRightOf(slotRect);
    }

    // 슬롯의 오른쪽 변 한가운데에 팝업의 피벗(좌측 중앙)을 붙인다.
    // 화면 좌표가 아니라 월드 좌표로 맞춘 뒤 간격만 로컬 단위로 더한다 -
    // 캔버스 스케일이 1이 아니어도 두 단위가 섞이지 않는다.
    private void PlaceRightOf(RectTransform slotRect)
    {
        slotRect.GetWorldCorners(_slotCorners);

        // GetWorldCorners: 0=좌하, 1=좌상, 2=우상, 3=우하.
        Vector3 rightEdgeCenter = (_slotCorners[2] + _slotCorners[3]) * 0.5f;

        Rect.position = rightEdgeCenter;
        Rect.anchoredPosition += new Vector2(_slotGap, 0f);
    }
}
