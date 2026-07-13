using UnityEngine;
using UnityEngine.UI;

// 건물 배치 및 철거, 재이동 디버깅용 -> 추후 수정될 수 있음
public class UI_BuildModeWindow : MonoBehaviour
{
    [System.Serializable]
    private struct BuildingButtonEntry
    {
        public Button Button;
        public Building Prefab;
    }

    // 필터 탭 하나. 선택되면 Menu_Focus가 활성, 아니면 Menu_Default가 활성이 된다.
    [System.Serializable]
    private struct FilterTab
    {
        public Button Button;
        public GameObject MenuFocus;
        public GameObject MenuDefault;
    }

    [SerializeField]
    private Button _buildButton;

    [SerializeField]
    private Button _removeButton;

    [SerializeField]
    private Button _moveButton;

    [SerializeField]
    private BuildingButtonEntry[] _buildingButtons;

    [SerializeField]
    private FilterTab[] _filterTabs;

    [SerializeField]
    private GameObject _buildModePanel;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    private void Awake()
    {
        // BuildMode 버튼: 열려 있으면 닫고, 닫혀 있으면 연다(토글).
        _buildButton.onClick.AddListener(ToggleBuildPanel);

        // 건물 먼저 선택 후 remove 버튼 클릭하면 건물 삭제되도록
        _removeButton.onClick.AddListener(() => _buildingPlacementController.RemoveSelectedBuilding());

        // 건물 먼저 선택 후 move 버튼 클릭 -> 새 위치 클릭하면 바로 이동
        _moveButton.onClick.AddListener(() => _buildingPlacementController.EnterMoveMode());

        foreach (BuildingButtonEntry entry in _buildingButtons)
        {
            Building prefab = entry.Prefab;
            entry.Button.onClick.AddListener(() => _buildingPlacementController.SelectBuilding(prefab));
        }

        for (int i = 0; i < _filterTabs.Length; i++)
        {
            int index = i; // 클로저 캡처용 지역 복사
            _filterTabs[i].Button.onClick.AddListener(() => SelectFilter(index));
        }

        // 시작 시 창은 닫힌 상태, 필터는 첫 탭이 선택된 상태로 초기화.
        _buildModePanel.SetActive(false);
        if (_filterTabs.Length > 0)
        {
            SelectFilter(0);
        }
    }

    private void Update()
    {
        // 이동 모드 진입/종료(클릭 이동, 취소, 우클릭 취소 등)에 맞춰 Move 버튼 표시를 매 프레임 동기화
        _moveButton.gameObject.SetActive(!_buildingPlacementController.IsMoving);
    }

    // BuildMode 버튼 토글: 상태를 뒤집고 그에 맞춰 배치 타일 표시/정리.
    private void ToggleBuildPanel()
    {
        bool willOpen = !_buildModePanel.activeSelf;
        _buildModePanel.SetActive(willOpen);

        if (willOpen)
        {
            _buildingPlacementController.ShowOccupiedTiles();
        }
        else
        {
            _buildingPlacementController.CancelAll();
            _buildingPlacementController.HideOccupiedTiles();
        }
    }

    // 선택된 탭만 Focus 상태로, 나머지는 Default 상태로 만든다.
    private void SelectFilter(int index)
    {
        for (int i = 0; i < _filterTabs.Length; i++)
        {
            bool isSelected = i == index;
            _filterTabs[i].MenuFocus.SetActive(isSelected);
            _filterTabs[i].MenuDefault.SetActive(!isSelected);
        }
    }
}
