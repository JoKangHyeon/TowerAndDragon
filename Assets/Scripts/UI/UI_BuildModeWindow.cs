using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 건물 배치 및 철거, 재이동 디버깅용 -> 추후 수정될 수 있음
public class UI_BuildModeWindow : MonoBehaviour
{
    // 필터 탭 하나. 선택되면 Menu_Focus가 활성, 아니면 Menu_Default가 활성이 된다.
    // 이 탭(카테고리)이 가진 건물들이 선택 시 슬롯 목록으로 생성된다.
    [System.Serializable]
    private struct FilterTab
    {
        public Button Button;
        public GameObject MenuFocus;
        public GameObject MenuDefault;
        public UI_BuildingSlot SlotPrefab;   // 이 카테고리 슬롯 프리팹 (Slot_Tower / Slot_Building / Slot_Factory)
        public Building[] Buildings;
    }

    // 상단 활성 버튼 묶음 (이동 / 철거).
    [System.Serializable]
    private struct ActiveButtons
    {
        public Button Move;
        public Button Remove;
    }

    [SerializeField]
    private Button _buildButton;

    [SerializeField]
    private ActiveButtons _activeButtons;

    [SerializeField]
    private FilterTab[] _filterTabs;

    [Header("건물 슬롯")]
    [Tooltip("생성된 슬롯이 들어갈 부모 (Scroll View의 Content). 슬롯 프리팹은 각 탭(FilterTab)에서 지정.")]
    [SerializeField]
    private Transform _slotContainer;

    [SerializeField]
    private GameObject _buildModePanel;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    [Header("패널 열림/닫힘 연출")]
    [SerializeField]
    private float _slideDuration = 0.5f;
    [Tooltip("열릴 때 시작 오프셋(홈 기준). 여기서 홈으로 슬라이드 인.")]
    [SerializeField]
    private Vector2 _openFromOffset = new Vector2(-50f, 0f);
    [Tooltip("닫힐 때 도착 오프셋(홈 기준). 홈에서 여기로 슬라이드 아웃 후 비활성화.")]
    [SerializeField]
    private Vector2 _closeToOffset = new Vector2(-500f, 0f);

    private RectTransform _panelRect;
    private Vector2 _homePos;
    private bool _isOpen;
    private Tween _panelTween;

    private readonly List<UI_BuildingSlot> _spawnedSlots = new();

    private void Awake()
    {
        // BuildMode 버튼: 열려 있으면 닫고, 닫혀 있으면 연다(토글).
        _buildButton.onClick.AddListener(ToggleBuildPanel);

        // 건물 먼저 선택 후 remove 버튼 클릭하면 건물 삭제되도록
        _activeButtons.Remove.onClick.AddListener(() => _buildingPlacementController.RemoveSelectedBuilding());

        // 건물 먼저 선택 후 move 버튼 클릭 -> 새 위치 클릭하면 바로 이동
        _activeButtons.Move.onClick.AddListener(() => _buildingPlacementController.EnterMoveMode());

        for (int i = 0; i < _filterTabs.Length; i++)
        {
            int index = i; // 클로저 캡처용 지역 복사
            _filterTabs[i].Button.onClick.AddListener(() => SelectFilter(index));
        }

        // 홈 위치(디자인된 최종 위치)를 숨기기 전에 기억해 둔다.
        _panelRect = _buildModePanel.GetComponent<RectTransform>();
        _homePos = _panelRect.anchoredPosition;

        // 시작 시 창은 닫힌 상태, 필터는 첫 탭이 선택된 상태로 초기화.
        _buildModePanel.SetActive(false);
        if (_filterTabs.Length > 0)
        {
            SelectFilter(0);
        }
    }

    private void Update()
    {
        Building selected = _buildingPlacementController.SelectedBuilding;

        // 이동 모드 진입/종료(클릭 이동, 취소, 우클릭 취소 등)에 맞춰 Move 버튼 표시를 매 프레임 동기화
        _activeButtons.Move.gameObject.SetActive(!_buildingPlacementController.IsMoving);

        // 이동/철거 불가 건물(성, 주둔지 등) 선택 시 버튼을 비활성화해 클릭해도 아무 반응 없는 상황을 방지
        _activeButtons.Move.interactable = selected != null && selected.IsMoveable;
        _activeButtons.Remove.interactable = selected != null && selected.IsRemoveable;
    }

    // BuildMode 버튼 토글.
    private void ToggleBuildPanel()
    {
        if (_isOpen)
            CloseBuildPanel();
        else
            OpenBuildPanel();
    }

    private void OpenBuildPanel()
    {
        _isOpen = true;
        _panelTween?.Kill();

        _buildModePanel.SetActive(true);
        // 홈에서 왼쪽으로 벗어난 위치에서 시작해 홈으로 슬라이드 인.
        _panelRect.anchoredPosition = _homePos + _openFromOffset;
        _panelTween = _panelRect.DOAnchorPos(_homePos, _slideDuration)
            .SetEase(Ease.OutBack)
            .SetLink(_buildModePanel);

        _buildingPlacementController.ShowOccupiedTiles();
    }

    private void CloseBuildPanel()
    {
        _isOpen = false;
        _buildingPlacementController.CancelAll();
        _buildingPlacementController.HideOccupiedTiles();

        // 홈에서 왼쪽으로 슬라이드 아웃한 뒤 패널을 비활성화한다.
        _panelTween?.Kill();
        _panelTween = _panelRect.DOAnchorPos(_homePos + _closeToOffset, _slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(_buildModePanel)
            .OnComplete(() => _buildModePanel.SetActive(false));
    }

    // 선택된 탭만 Focus 상태로, 나머지는 Default 상태로 만들고, 그 탭의 건물 슬롯 목록을 다시 생성한다.
    private void SelectFilter(int index)
    {
        for (int i = 0; i < _filterTabs.Length; i++)
        {
            bool isSelected = i == index;
            _filterTabs[i].MenuFocus.SetActive(isSelected);
            _filterTabs[i].MenuDefault.SetActive(!isSelected);
        }

        RebuildSlots(_filterTabs[index].SlotPrefab, _filterTabs[index].Buildings);
    }

    // 컨테이너의 기존 슬롯을 지우고, 주어진 슬롯 프리팹으로 건물 목록만큼 슬롯을 새로 생성한다.
    private void RebuildSlots(UI_BuildingSlot slotPrefab, Building[] buildings)
    {
        foreach (UI_BuildingSlot slot in _spawnedSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        _spawnedSlots.Clear();

        if (slotPrefab == null || _slotContainer == null || buildings == null)
            return;

        foreach (Building building in buildings)
        {
            UI_BuildingSlot slot = Instantiate(slotPrefab, _slotContainer);
            slot.Setup(building, OnSlotSelected);
            _spawnedSlots.Add(slot);
        }
    }

    // 슬롯 클릭 시 해당 건물을 배치 대상으로 선택 (기존 building buttons에서 옮겨온 기능).
    private void OnSlotSelected(Building prefab)
    {
        _buildingPlacementController.SelectBuilding(prefab);
    }
}
