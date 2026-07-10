using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 건물 배치 및 철거, 재이동 디버깅용 -> 추후 수정될 수 있음
public class ExampleBuildingUI : MonoBehaviour
{
    [System.Serializable]
    private struct BuildingButtonEntry
    {
        public Button Button;
        public Building Prefab;
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
    private GameObject _buildModePanel;

    [SerializeField]
    private GameObject _selectBuildingModePanel;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    private void Awake()
    {
        _buildButton.onClick.AddListener(() =>
        {
            _buildModePanel.gameObject.SetActive(true);
            _selectBuildingModePanel.gameObject.SetActive(true);
        });

        // 건물 먼저 선택 후 remove 버튼 클릭하면 건물 삭제되도록
        _removeButton.onClick.AddListener(() => _buildingPlacementController.RemoveSelectedBuilding());

        // 건물 먼저 선택 후 move 버튼 클릭 -> 새 위치 클릭하면 바로 이동
        _moveButton.onClick.AddListener(() => _buildingPlacementController.EnterMoveMode());

        foreach (BuildingButtonEntry entry in _buildingButtons)
        {
            Building prefab = entry.Prefab;
            entry.Button.onClick.AddListener(() => _buildingPlacementController.SelectBuilding(prefab));
        }
    }

    private void Update()
    {
        // 이동 모드 진입/종료(클릭 이동, 취소, 우클릭 취소 등)에 맞춰 Move/Cancel 버튼 표시를 매 프레임 동기화
        _moveButton.gameObject.SetActive(!_buildingPlacementController.IsMoving);

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            _buildModePanel.gameObject.SetActive(false);
            _selectBuildingModePanel.gameObject.SetActive(false);
        }
    }
}
