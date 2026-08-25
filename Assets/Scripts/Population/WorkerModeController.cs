using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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
public class WorkerModeController : MonoBehaviour, IExclusiveMode, IExclusiveModeEntryGuard
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

    [Tooltip("랜드마크에도 인구를 배치할 수 있게 참조한다. 비워두면 건물만 대상이 된다.")]
    [WiringOptional]
    [SerializeField]
    private LandmarkManager _landmarkManager;

    [Tooltip("인구를 배치할 건물이 하나도 없을 때 안내 메시지를 띄운다. 없으면 조용히 진입만 막는다.")]
    [WiringOptional]
    [SerializeField]
    private UI_WarningWindow _warningWindow;

    [Tooltip("성에서 캐릭터가 걸어 나오는 연출. 비워두면 연출만 생략되고 배치 자체는 그대로 동작한다.")]
    [SerializeField]
    private VillagerDispatchSystem _villagerDispatch;

    [Tooltip("인구를 배치하는 액션 - 건설 확정과 공유하는 좌클릭 액션(Confirm).")]
    [SerializeField]
    private InputActionReference _assignAction;

    [Tooltip("인구 배치 모드를 닫는 액션(ESC).")]
    [SerializeField]
    private InputActionReference _closeAction;

    [Tooltip("한 번에 최대치를 옮기는 보정키(Shift) - Player/BulkModifier.")]
    [SerializeField]
    private InputActionReference _bulkModifierAction;

    [Tooltip("한 번에 묶음 단위로 옮기는 보정키(Ctrl) - Player/FineModifier.")]
    [SerializeField]
    private InputActionReference _fineModifierAction;

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
    private readonly List<(Vector3 WorldPosition, IPopulationAllocationTarget Target)> _targetBuffer = new();
    private readonly List<Building> _tintedBuildings = new();

    private (List<Vector3Int> Coords, Color Color)[] _dayHighlightGroups;
    private (List<Vector3Int> Coords, Color Color)[] _nightHighlightGroups;

    // 밤에는 조작을 잠근다 - UI_PopulationAllocationWindow.IsDay와 같은 fail-closed 판정
    // (CycleManager가 없으면 조작 불가로 본다).
    private bool IsDay =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    // 보정키는 눌린 상태를 조회만 한다. 액션을 켜는 것은 GlobalInputBootstrap이 맡는다 -
    // 이 컨트롤러는 자신의 활성 여부를 스스로 판단하는 구조라 액션 수명을 여기에 묶지 않는다.
    private bool IsBulkModifierPressed =>
        _bulkModifierAction != null && _bulkModifierAction.action.IsPressed();

    private bool IsFineModifierPressed =>
        _fineModifierAction != null && _fineModifierAction.action.IsPressed();

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

        // 영혼 타워처럼 다른 타워의 정원에 영향을 주는 건물이 새로 지어지거나 철거될 때는
        // PopulationChanged가 발화하지 않는다(PopulationManager.TryCreateAllocation는 알리지 않고,
        // TryReleaseAll은 배치 인구가 0이면 조기 반환한다) - 그리드 변화 자체를 구독해 덮는다.
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingChanged);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingChanged);
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

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingChanged);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingChanged);
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

        // 진입 관문은 OpenExclusive가 CanEnterNow로 물어본다 - 단축키와 같은 한 곳에서 판정된다.
        // UIManager가 없는 씬에서만 여기서 직접 묻는다.
        if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else if (CanEnterNow())
        {
            SetWorkerModeActive(true);
        }
    }

    /// <summary>
    /// 밤에도 진입은 허용한다 - 배치 현황을 보는 것 자체는 막을 이유가 없고 조작만 잠긴다(<see cref="IsDay"/>).
    /// 다만 인구를 넣을 수 있는 건물이 하나도 없으면 칠할 건물도 누를 대상도 없으므로,
    /// 아무 반응 없이 켜졌다 꺼지는 대신 이유를 알려주고 진입하지 않는다.
    /// </summary>
    public bool CanEnterNow()
    {
        if (HasAnyPopulationTarget())
        {
            return true;
        }

        _warningWindow?.Show(UI_WarningWindow.MessageId.WorkerMode);
        return false;
    }

    // 인구를 넣을 수 있는 건물이 하나라도 있는지. 판정 기준은 RefreshOverlays가 대상으로 삼는 것과 같다
    // (초기화된 IPopulationAllocationTarget + 정원 1명 이상) - 두 기준이 어긋나면
    // "진입은 되는데 아무것도 안 칠해지는" 상태가 생긴다.
    // 그리드가 연결되지 않아 판정할 수 없으면 막지 않는다.
    private bool HasAnyPopulationTarget()
    {
        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return true;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            var target = building.GetComponent<IPopulationAllocationTarget>();

            if (target != null && target.IsInitialized && target.Capacity > 0)
            {
                return true;
            }
        }

        // 건물이 하나도 없어도 점령한 랜드마크가 있으면 진입할 수 있어야 한다 -
        // RefreshOverlays가 그 랜드마크에 라벨을 띄우므로 "진입은 됐는데 아무것도 없는" 상태가 아니다.
        if (_landmarkManager != null)
        {
            foreach (Landmark landmark in _landmarkManager.Landmarks)
            {
                if (IsAssignableLandmark(landmark))
                {
                    return true;
                }
            }
        }

        return false;
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

        if (_uiManager != null && !_uiManager.CanCloseExclusive(this))
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

        // 연출은 "실제로 몇 명이 움직였는지"를 조작 전후 값의 차이로 관측한다 - 클램프 규칙
        // (PopulationAssignmentRules)을 여기서 다시 계산하면 규칙이 두 곳으로 갈라지기 때문이다.
        int assignedBefore = target.AssignedPopulation;

        if (PopulationAssignmentRules.TryAssignClamped(target, _populationManager, requested) &&
            _villagerDispatch != null)
        {
            _villagerDispatch.NotifyAllocationChanged(target, assignedBefore);
        }
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
        int assignedBefore = target.AssignedPopulation;

        if (PopulationAssignmentRules.TryUnassignClamped(target, requested) &&
            _villagerDispatch != null)
        {
            _villagerDispatch.NotifyAllocationChanged(target, assignedBefore);
        }
    }

    // Shift는 최대치(호출자가 넘긴 남은 정원 또는 현재 배치 인원), Ctrl은 묶음 단위, 그 외 한 명.
    // 둘 다 눌렸으면 Shift가 우선한다.
    private int ResolveRequestedAmount(int maxAmount)
    {
        if (IsBulkModifierPressed)
            return maxAmount;

        return IsFineModifierPressed ? POPULATION_STEP_BULK : POPULATION_STEP_SINGLE;
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

        // 건물이 우선이다 - 랜드마크는 청크 전체가 판정 범위라, 그 안에 지은 건물까지
        // 삼켜버리면 정작 건물에 인구를 넣을 수 없게 된다.
        if (building != null)
        {
            target = building.GetComponent<IPopulationAllocationTarget>();
            return target != null && target.IsInitialized;
        }

        return TryGetLandmarkTargetAt(hoveredCell, out target);
    }

    // 빈 칸을 클릭했을 때, 그 칸이 속한 청크의 랜드마크를 대상으로 삼는다.
    // 랜드마크는 발자국이 없어 정확한 칸을 짚을 수 없으므로 청크 전체를 판정 범위로 쓴다.
    private bool TryGetLandmarkTargetAt(Vector3Int cell, out IPopulationAllocationTarget target)
    {
        target = null;

        if (_landmarkManager == null)
        {
            return false;
        }

        Chunk chunk = _gridMap.GetChunkAt(cell);

        if (chunk == null ||
            !_landmarkManager.TryGetLandmarkAt(chunk.ChunkCoord, out Landmark landmark) ||
            !IsAssignableLandmark(landmark))
        {
            return false;
        }

        target = landmark.Population;
        return true;
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

            _targetBuffer.Add((WorkerCountOverlayRenderer.ResolveLabelPosition(building), target));
            AddFootprintCoords(building, ResolveBuffer(state));

            building.SetHighlighted(true, ResolveBuildingTint(state));
            _tintedBuildings.Add(building);
        }

        AddLandmarkTargets();

        _mouseSelectController.HighlightCellGroups(HighlightGroups);

        if (_countOverlay != null)
        {
            _countOverlay.Refresh(_targetBuffer, _countLabelColor);
        }
    }

    // 랜드마크는 Building이 아니라 격자 칸을 차지하지 않으므로, 건물처럼 발자국을 칠하거나
    // 스프라이트를 물들이지 않는다. 청크 중심에 배치/정원 라벨만 띄우고, 그 라벨이 곧
    // "여기에 인구를 넣을 수 있다"는 표시가 된다.
    private void AddLandmarkTargets()
    {
        if (_landmarkManager == null)
        {
            return;
        }

        foreach (Landmark landmark in _landmarkManager.Landmarks)
        {
            if (!IsAssignableLandmark(landmark))
            {
                continue;
            }

            _targetBuffer.Add((
                _gridMap.GetChunkCenterWorld(landmark.ChunkCoord),
                landmark.Population));
        }
    }

    // 아직 점령하지 않은 랜드마크는 인구를 넣을 수 없으므로 표시도 하지 않는다
    // (LandmarkPopulation.CanChangePopulation과 같은 기준).
    private bool IsAssignableLandmark(Landmark landmark)
    {
        return landmark.IsConquered &&
            landmark.Population != null &&
            landmark.Population.IsInitialized &&
            landmark.Population.Capacity > 0;
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

    // 새로 지은/철거되는 건물이 영혼 타워처럼 다른 타워의 정원(SoulPopulation)에 영향을 줄 수
    // 있다 - 그 건물 자신이 배치 대상인지와 무관하게 항상 전체를 다시 그린다.
    //
    // 한 프레임 미룬다(CLAUDE.md 이벤트 초기화 규칙과 같은 이유) - 이 프레임 안에서는 아직
    // 두 시점이 어긋나 있다: 건설 직후에는 TowerPopulationCoordinator의 Initialize가
    // 같은 OnBuildingAdded 호출 안에서 이 리스너보다 나중에 돌 수 있어 방금 지은 타워 자신이
    // 아직 IsInitialized=false로 보이고, 철거 시에는 OnBuildingRemoving이 그리드에서 실제로
    // 빠지기 "전"에 발화해 철거되는 타워의 오라가 아직 살아있는 값으로 잡힌다. 다음 프레임이면
    // 두 경우 모두 정리가 끝나 있다.
    private void HandleBuildingChanged(Building building)
    {
        if (IsActive)
        {
            RefreshOverlaysNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    private async UniTaskVoid RefreshOverlaysNextFrameAsync(CancellationToken token)
    {
        await UniTask.Yield(token);

        if (IsActive)
        {
            RefreshOverlays();
        }
    }

    private static Vector2 PointerScreenPosition() =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    bool IExclusiveMode.IsOpen => IsActive;
    void IExclusiveMode.Open() => SetWorkerModeActive(true);
    void IExclusiveMode.Close() => SetWorkerModeActive(false);
}
