using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 인구 배치 모드(Worker Mode, 이슈 #137). 창을 열지 않고 화면에서 건물을 직접 클릭해
// 인구를 배치/회수하는 모드다 - 여러 건물을 연속으로 클릭해 한 번에 재배분하는 것이 목적이므로,
// 이 모드에서는 건물을 "선택"하지 않는다(선택 창 Building_window은 InputSuppressed 동안 스스로 숨는다).
//   클릭 = 1명 배치 / 우클릭 = 1명 회수
//   Shift+클릭·우클릭 = 최대치까지 배치 / 전원 회수
//   Ctrl+클릭·우클릭 = POPULATION_STEP_BULK명 단위
// 모드 진입/입력 처리 구조는 ConquestModeController와 동일하다.
// _assignAction("Confirm")은 BuildingPlacementController._placeAction과 같은 공유 액션이며
// GlobalInputBootstrap이 한 번만 Enable한다 - 이 컨트롤러는 스스로 Enable/Disable하지 않는다.
public class WorkerModeController : MonoBehaviour, IExclusiveMode
{
    private const int POPULATION_STEP_SINGLE = 1;
    private const int POPULATION_STEP_BULK = 5;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    [SerializeField]
    private PopulationManager _populationManager;

    [SerializeField]
    private CycleManager _cycleManager;

    [Tooltip("모드 진입 시 다른 배타 모드(건설·점령 등)를 닫기 위해 거치는 관리자.")]
    [SerializeField]
    private UIManager _uiManager;

    [Tooltip("건물 위에 배치/정원 숫자를 띄우는 오버레이.")]
    [SerializeField]
    private WorkerCountOverlayRenderer _countOverlay;

    [Tooltip("인구를 배치하는 액션 - 건설 확정과 공유하는 좌클릭 액션(Confirm).")]
    [SerializeField]
    private InputActionReference _assignAction;

    [Tooltip("인구 배치 모드를 닫는 액션(ESC).")]
    [SerializeField]
    private InputActionReference _closeAction;

    [Tooltip("인구가 한 명도 없어 정지한 건물 색.")]
    [SerializeField]
    private Color _idleHighlightColor = Color.red;

    [Tooltip("인구를 더 넣을 자리가 남은 건물 색.")]
    [SerializeField]
    private Color _hasRoomHighlightColor = Color.green;

    [Tooltip("정원이 꽉 찬 건물 색.")]
    [SerializeField]
    private Color _fullHighlightColor = Color.cyan;

    [Tooltip("밤에는 조작이 잠기므로 모든 건물을 이 색 하나로 표시한다.")]
    [SerializeField]
    private Color _nightLockedHighlightColor = Color.gray;

    // 타일 하이라이트(TileHighlight)는 얇은 외곽선 스프라이트라 건물이 올라간 칸에서는 거의 가려진다.
    // 그래서 건물 스프라이트 자체도 상태별로 물들여(BuildingPlacementController의 선택 표시와 같은 API)
    // 어느 건물이 비어 있는지 한눈에 보이게 한다. 형태를 알아볼 수 있도록 옅은 색을 쓴다.
    [Header("건물 스프라이트 색조 - 상태 구분용")]
    [Tooltip("인구가 없어 정지한 건물 색조.")]
    [SerializeField]
    private Color _idleBuildingTint = new Color(1f, 0.55f, 0.55f);

    [Tooltip("인구를 더 넣을 자리가 남은 건물 색조.")]
    [SerializeField]
    private Color _hasRoomBuildingTint = new Color(0.6f, 1f, 0.6f);

    [Tooltip("정원이 꽉 찬 건물 색조.")]
    [SerializeField]
    private Color _fullBuildingTint = new Color(0.6f, 0.9f, 1f);

    [Tooltip("밤에 조작이 잠겼을 때의 건물 색조.")]
    [SerializeField]
    private Color _nightLockedBuildingTint = new Color(0.7f, 0.7f, 0.7f);

    [Tooltip("건물 위 숫자 라벨 색.")]
    [SerializeField]
    private Color _countLabelColor = Color.white;

    [Tooltip("누른 뒤 이 픽셀 이상 포인터가 움직이면 클릭이 아니라 카메라 드래그로 간주해 배치를 무시한다.")]
    [SerializeField]
    private float _dragThreshold = 10f;

    public bool IsActive { get; private set; }

    private Vector2 _pressScreenPosition;

    private readonly List<Vector3Int> _idleBuffer = new();
    private readonly List<Vector3Int> _hasRoomBuffer = new();
    private readonly List<Vector3Int> _fullBuffer = new();
    private readonly List<(Building Building, IPopulationAllocationTarget Target)> _targetBuffer = new();
    private readonly List<Building> _tintedBuildings = new();

    private (List<Vector3Int> Coords, Color Color)[] _dayHighlightGroups;
    private (List<Vector3Int> Coords, Color Color)[] _nightHighlightGroups;

    // 밤에는 조작을 잠근다 - UI_PopulationAllocationWindow.IsDay와 같은 fail-closed 판정
    // (CycleManager가 없으면 조작 불가로 본다).
    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private static bool IsShiftPressed =>
        Keyboard.current != null &&
        (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);

    private static bool IsCtrlPressed =>
        Keyboard.current != null &&
        (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

    // 하이라이트 묶음은 버퍼를 그대로 물고 있으므로 한 번만 만들어 재사용한다.
    // Awake가 아니라 첫 사용 시점에 만들어, 초기화 순서와 무관하게 항상 준비된 상태를 보장한다.
    private (List<Vector3Int> Coords, Color Color)[] HighlightGroups
    {
        get
        {
            if (_dayHighlightGroups == null)
            {
                _dayHighlightGroups = new (List<Vector3Int> Coords, Color Color)[]
                {
                    (_idleBuffer, _idleHighlightColor),
                    (_hasRoomBuffer, _hasRoomHighlightColor),
                    (_fullBuffer, _fullHighlightColor)
                };

                // 밤에는 상태 구분 없이 한 색으로 칠해 "지금은 조작할 수 없다"를 드러낸다.
                _nightHighlightGroups = new (List<Vector3Int> Coords, Color Color)[]
                {
                    (_idleBuffer, _nightLockedHighlightColor),
                    (_hasRoomBuffer, _nightLockedHighlightColor),
                    (_fullBuffer, _nightLockedHighlightColor)
                };
            }

            return IsDay ? _dayHighlightGroups : _nightHighlightGroups;
        }
    }

    private void OnEnable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(HandleCycleChanged);
        }
    }

    private void OnDisable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(HandleCycleChanged);
        }
    }

    private void Update()
    {
        if (!IsActive)
            return;

        HandleAssignInput();
        HandleUnassignInput();
        HandleCloseInput();
    }

    // 버튼이 호출하는 진입점 - 누를 때마다 모드를 켜고 끈다
    // (UI_ConquestWindow.ToggleConquestMode와 같은 역할).
    // 밤에도 진입은 허용한다 - 배치 현황을 보는 것 자체는 막을 이유가 없고, 조작만 잠긴다.
    public void ToggleWorkerMode()
    {
        if (IsActive)
        {
            SetWorkerModeActive(false);
            return;
        }

        if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            SetWorkerModeActive(true);
        }
    }

    public void SetWorkerModeActive(bool isActive)
    {
        IsActive = isActive;

        // 이 모드 동안에는 건물 배치 컨트롤러가 같은 클릭을 처리해 건물을 선택하거나
        // 우클릭으로 선택을 해제하지 못하도록 입력을 억제한다.
        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.InputSuppressed = isActive;
        }

        if (isActive)
        {
            if (_buildingPlacementController != null)
            {
                _buildingPlacementController.CancelAll();
            }

            RefreshOverlays();
        }
        else
        {
            _mouseSelectController.ClearHighlights();
            ClearBuildingTints();

            if (_countOverlay != null)
            {
                _countOverlay.Clear();
            }
        }
    }

    private void HandleCloseInput()
    {
        if (_closeAction == null || !_closeAction.action.WasPerformedThisFrame())
            return;

        SetWorkerModeActive(false);
    }

    // 좌클릭 배치. 좌클릭 드래그는 CameraController가 카메라 이동에 쓰므로,
    // 누른 지점에서 임계값 이상 움직였으면 배치로 보지 않는다(점령 모드와 같은 판정).
    private void HandleAssignInput()
    {
        if (_assignAction == null)
            return;

        if (_assignAction.action.WasPressedThisFrame())
        {
            _pressScreenPosition = PointerScreenPosition();
        }

        if (!_assignAction.action.WasReleasedThisFrame())
            return;

        if (Vector2.Distance(_pressScreenPosition, PointerScreenPosition()) > _dragThreshold)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!TryGetTargetUnderPointer(out IPopulationAllocationTarget target))
            return;

        int requested = ResolveRequestedAmount(target.AvailableCapacity);
        PopulationAssignmentRules.TryAssignClamped(target, _populationManager, requested);
    }

    // 우클릭 회수. 우클릭은 카메라 드래그에 쓰이지 않아 누른 시점에 바로 판정한다
    // (BuildingPlacementController.HandleBuildCancelInput과 같은 방식).
    private void HandleUnassignInput()
    {
        if (Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        if (!TryGetTargetUnderPointer(out IPopulationAllocationTarget target))
            return;

        int requested = ResolveRequestedAmount(target.AssignedPopulation);
        PopulationAssignmentRules.TryUnassignClamped(target, requested);
    }

    // Shift는 최대치(호출자가 넘긴 남은 정원 또는 현재 배치 인원), Ctrl은 묶음 단위, 그 외 한 명.
    // 둘 다 눌렸으면 Shift가 우선한다.
    private static int ResolveRequestedAmount(int maxAmount)
    {
        if (IsShiftPressed)
            return maxAmount;

        return IsCtrlPressed ? POPULATION_STEP_BULK : POPULATION_STEP_SINGLE;
    }

    // 포인터 아래 건물이 인구를 넣을 수 있는 대상인지 판정한다.
    // 인구를 쓰지 않는 건물(성, 새끼용 타워 등)은 IPopulationAllocationTarget이 없어 걸러진다.
    private bool TryGetTargetUnderPointer(out IPopulationAllocationTarget target)
    {
        target = null;

        if (!IsDay)
            return false;

        Vector3Int hoveredCell = _mouseSelectController.GetHoveredCell();
        Building building = _gridMap.GetBuildingAt(hoveredCell);
        if (building == null)
            return false;

        target = building.GetComponent<IPopulationAllocationTarget>();

        return target != null && target.IsInitialized;
    }

    // 인구를 넣을 수 있는 건물 전부를 배치 상태별 색으로 칠하고, 그 위에 배치/정원 숫자를 띄운다.
    // 모드 진입 시, 인구가 바뀔 때, 낮밤이 바뀔 때 다시 계산한다.
    private void RefreshOverlays()
    {
        _idleBuffer.Clear();
        _hasRoomBuffer.Clear();
        _fullBuffer.Clear();
        _targetBuffer.Clear();
        ClearBuildingTints();

        foreach (Building building in _gridMap.Buildings)
        {
            var target = building.GetComponent<IPopulationAllocationTarget>();
            if (target == null || !target.IsInitialized || target.Capacity <= 0)
                continue;

            StaffingState state = ResolveState(target);

            _targetBuffer.Add((building, target));
            AddFootprintCoords(building, ResolveBuffer(state));

            building.SetHighlighted(true, ResolveBuildingTint(state));
            _tintedBuildings.Add(building);
        }

        _mouseSelectController.HighlightCellGroups(HighlightGroups);

        if (_countOverlay != null)
        {
            _countOverlay.Refresh(_targetBuffer, _countLabelColor);
        }
    }

    private enum StaffingState
    {
        Idle,
        HasRoom,
        Full
    }

    private static StaffingState ResolveState(IPopulationAllocationTarget target)
    {
        if (target.AssignedPopulation == 0)
            return StaffingState.Idle;

        return target.AvailableCapacity == 0 ? StaffingState.Full : StaffingState.HasRoom;
    }

    private List<Vector3Int> ResolveBuffer(StaffingState state) => state switch
    {
        StaffingState.Idle => _idleBuffer,
        StaffingState.Full => _fullBuffer,
        _ => _hasRoomBuffer
    };

    private Color ResolveBuildingTint(StaffingState state)
    {
        if (!IsDay)
            return _nightLockedBuildingTint;

        return state switch
        {
            StaffingState.Idle => _idleBuildingTint,
            StaffingState.Full => _fullBuildingTint,
            _ => _hasRoomBuildingTint
        };
    }

    // 상태 색조를 걷어낸다 - 모드 종료 시와 다시 칠하기 직전에 호출한다.
    private void ClearBuildingTints()
    {
        for (int i = 0; i < _tintedBuildings.Count; i++)
        {
            if (_tintedBuildings[i] != null)
            {
                _tintedBuildings[i].SetHighlighted(false, default);
            }
        }

        _tintedBuildings.Clear();
    }

    private void AddFootprintCoords(Building building, List<Vector3Int> target)
    {
        IReadOnlyList<Vector3Int> coords = _gridMap.GetFootprintCoords(building);
        for (int i = 0; i < coords.Count; i++)
        {
            target.Add(coords[i]);
        }
    }

    private void HandlePopulationChanged(PopulationState state)
    {
        if (IsActive)
            RefreshOverlays();
    }

    private void HandleCycleChanged(CycleManager.CycleState state)
    {
        if (IsActive)
            RefreshOverlays();
    }

    private static Vector2 PointerScreenPosition() =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    bool IExclusiveMode.IsOpen => IsActive;
    void IExclusiveMode.Open() => SetWorkerModeActive(true);
    void IExclusiveMode.Close() => SetWorkerModeActive(false);
}
