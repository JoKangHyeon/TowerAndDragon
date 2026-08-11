using System.Collections.Generic;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildingPlacementController : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private InputActionReference _placeAction;
    [SerializeField]
    private InputActionReference _cancelMoveAction;

    [SerializeField]
    private InputActionReference _rotateAction;

    [SerializeField]
    private ResourceManager _resourceManager;

    [SerializeField]
    private CycleManager _cycleManager;

    [Tooltip("타워 선택 시 공격 사거리를 타원으로 표시할 인디케이터.")]
    [SerializeField]
    private RangeIndicator _rangeIndicator;

    [Tooltip("오라 타워 또는 새끼용 선택 시 버프 반경을 타원으로 표시할 인디케이터. 공격 사거리와 별개 원이라 서로 다른 색으로 구분해둘 것.")]
    [SerializeField]
    private RangeIndicator _buffRangeIndicator;

    [Tooltip("버프 반경 안에서 이 지형들의 지역 페널티(생산·공속·유지비)를 전부 무효화한다. 얼음의 건설 해제와는 별개 메커니즘 - 시간=사막.")]
    [SerializeField] private TerrainType[] _penaltyMitigationTerrains;
    public IReadOnlyList<TerrainType> PenaltyMitigationTerrains => _penaltyMitigationTerrains;

    [Tooltip("용암 지대처럼 지형 때문에 건설이 막혔을 때 경고를 띄울 창. 비어 있으면 안내만 생략되고 판정은 그대로다.")]
    [SerializeField]
    private UI_WarningWindow _warningWindow;

    [Tooltip("이 지형 때문에 막히면 '얼음 새끼용의 범위가 필요하다'고 안내한다. BD_Ice의 _constructionUnlockTerrains와 같게 유지할 것 - 절벽·물처럼 어떤 새끼용으로도 풀 수 없는 지형을 여기 넣으면 안내가 거짓말이 된다.")]
    [SerializeField]
    private TerrainType _iceUnlockableTerrain = TerrainType.Volcano;


    [Tooltip("타워를 이만큼(초) 꾹 누르고 있으면 이동 모드로 진입한다.")]
    [SerializeField]
    private float _moveHoldDuration = 1f;

    [Tooltip("누른 뒤 이 픽셀 이상 포인터가 움직이면 클릭이 아니라 드래그(카메라 패닝)로 간주해 배치/선택을 무시한다.")]
    [SerializeField]
    private float _dragThreshold = 10f;

    // 건물 철거 시 건설 비용 중 돌려주는 비율 - 낮밤 사이클이 한 번도 돌지 않은 당일 철거는 전액, 그 외엔 일부만 환급.
    private const float DEMOLISH_REFUND_RATIO_SAME_DAY = 1f;
    private const float DEMOLISH_REFUND_RATIO_LATE = 0.7f;

    private Building _selectedBuilding;
    private Vector3Int? _selectedExistingBuildingCoord;
    private Vector3Int? _moveSourceCoord;
    private bool _isOccupiedOverlayVisible;

    private float _holdTimer;
    private Vector3Int? _holdCoord;

    private Vector2 _pressScreenPosition;
    // 이 누름이 이 컨트롤러의 것으로 유효한가. UI 위에서 시작했거나, 억제 중이라 누름 프레임을
    // 아예 보지 못한 누름은 뗄 때 확정되지 않아야 한다.
    private bool _isPressValid;
    // 롱프레스가 이 누름을 이동 모드 진입으로 이미 소비했다. 뗄 때 확정을 한 번 더 하지 않도록 막는다.
    private bool _longPressConsumedPress;

    private readonly List<IBuildModeInteractionQuery> _interactionQueries = new();

    // 클릭으로 선택이 확정될 때마다 발화한다(선택 해제면 null). 같은 건물을 다시 눌러도 발화하므로
    // SelectedBuilding 폴링과 달리 "재클릭"을 놓치지 않는다.
    // 필드 초기화 시점에 생성하므로 구독자의 Awake/OnEnable 순서와 무관하게 안전하다.
    public UnityEvent<Building> BuildingSelected = new();

    public bool IsMoving => _moveSourceCoord.HasValue;

    // 지금 마우스를 따라다니는 배치 대기 프리팹. 그리드 위에서 고른 기존 건물(SelectedBuilding)과는 다른 것이다.
    public Building BuildingToPlace => _selectedBuilding;

    // 배치 대기 프리팹이 바뀔 때 알린다 - 선택·취소·배치 완료가 모두 SetBuildingToPlace 한 곳을 지난다.
    // 안내 오버레이가 "무엇을 지을지 고르기 전"과 "타일을 찍기 전"을 구분하는 데 쓴다.
    public UnityEvent<Building> BuildingToPlaceChanged = new();

    // 그리드에 이미 있는 건물을 클릭해 고른 대상이 바뀔 때 알린다(고른 게 없어지면 null).
    // 인구 패널은 열림/닫힘 훅이 없어서, 안내가 "건물을 클릭했다"를 잡을 유일한 창구다.
    public UnityEvent<Building> SelectedBuildingChanged = new();

    // 상태에서 파생시켜 비교하므로 알림이 실제 선택과 어긋날 일이 없다.
    private Building _notifiedSelection;

    // 건설/철거 공통 낮 판정 - CycleManager가 배선되지 않은 씬은 무제한 허용한다
    // (BabyDragonTower.IsMoveable과 같은 fail-open 관례).
    public bool IsDayForBuildActions =>
        _cycleManager == null || _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    // 철거 버튼 interactable 판정에 쓴다 - IsRemoveable(건물 종류) AND 낮(시점) 둘 다 만족해야 한다.
    public bool CanRemoveNow(Building building) =>
        building != null && building.IsRemoveable && IsDayForBuildActions;

    // 이동 버튼 interactable 판정 및 실제 이동 진입 판정에 쓴다 - IsMoveable(건물 종류 + 이동 예산) AND
    // (낮(시점) OR 건물이 밤 이동을 예외적으로 허용). 시간 새끼용 공격 모드가 후자에 해당한다.
    public bool CanMoveNow(Building building) =>
        building != null && building.IsMoveable && (IsDayForBuildActions || building.CanMoveAtNight);

    // 점령 모드 등 다른 모드가 켜져 있을 때 이 컨트롤러의 클릭 처리를 막는다.
    // (컴포넌트를 비활성화하면 공유 입력 액션까지 Disable되므로, 입력만 선택적으로 억제한다.)
    public bool InputSuppressed { get; set; }

    public void AddInteractionQuery(IBuildModeInteractionQuery query)
    {
        if (query != null && !_interactionQueries.Contains(query))
        {
            _interactionQueries.Add(query);
        }
    }

    public void RemoveInteractionQuery(IBuildModeInteractionQuery query) =>
        _interactionQueries.Remove(query);

    public Building SelectedBuilding
    {
        get
        {
            if (_selectedExistingBuildingCoord.HasValue)
                return _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);

            if (_moveSourceCoord.HasValue)
                return _gridMap.GetBuildingAt(_moveSourceCoord.Value);

            return null;
        }
    }

    // _placeAction("Confirm")은 ConquestModeController와 공유하는 액션이라 GlobalInputBootstrap이
    // 게임 시작 시 한 번만 Enable한다 - 여기서 다시 Enable/Disable하면 그 컨트롤러까지 영향을 준다.
    // _cancelMoveAction/_rotateAction은 이 컨트롤러 전용이라 여기서 직접 관리한다.
    private void OnEnable()
    {
        if (_cancelMoveAction != null)
            _cancelMoveAction.action.Enable();

        if (_rotateAction != null)
            _rotateAction.action.Enable();
    }

    private void Awake()
    {
        if (_gridMap == null)
        {   Debug.LogWarning($"[BuildingPlacementController] GridMap 인스펙터 연결 필요");
            return;
        }
        if (_mouseSelectController == null)
        {   Debug.LogWarning($"[BuildingPlacementController] MouseSelectController 인스펙터 연결 필요");
            return;
        }

        if (_placeAction == null)
        {   Debug.LogWarning($"[BuildingPlacementController] PlaceAction 인스펙터 연결 필요");
            return;
        }

        if (_cancelMoveAction == null)
        {   Debug.LogWarning($"[BuildingPlacementController] CancelMoveAction 인스펙터 연결 필요");
            return;
        }

        _gridMap.OnCellChanged.AddListener(HandleCellChanged);
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
    }

    private void HandleCellChanged(GridCell cell) => RefreshOccupiedOverlay();

    private void Update()
    {
        RefreshSelectedRangeIndicator();

        // 다른 모드(점령·스킬 타겟팅 등)가 클릭을 점유 중이면 건물 배치/선택 입력을 처리하지 않는다.
        if (InputSuppressed)
        {
            // 억제 중에 진행된 누름은 이 컨트롤러의 것이 아니다 - 억제가 풀린 뒤 뗄 때
            // 남은 상태로 확정되지 않도록 여기서 지운다.
            _isPressValid = false;
            _holdCoord = null;
            return;
        }

        // HandleLongPressMove가 먼저다 - 누름 프레임에 _longPressConsumedPress를 초기화해야
        // 뗄 때 HandlePlacementInput이 이번 누름의 값을 읽는다.
        HandleLongPressMove();
        HandlePlacementInput();
        HandleBuildCancelInput();
        HandleCancelInput();
        HandleRotateInput();
    }

    // 슬롯을 골라 배치 미리보기 중일 때 우클릭하면 배치를 취소한다(선택 해제 + 미리보기 종료).
    private void HandleBuildCancelInput()
    {
        if (_selectedBuilding == null || !CanCancelPlacement())
            return;

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            CancelBuildMode();
    }

    // 배치/이동 미리보기 중일 때만 회전을 적용한다 - 이동 버튼을 누르지 않고 건물만 선택한 상태에서는
    // 회전이 적용되지 않는다(제자리 회전은 지원하지 않음, 회전하려면 이동 모드로 들어가야 함).
    private void HandleRotateInput()
    {
        if (_rotateAction == null ||
            !_rotateAction.action.WasPerformedThisFrame() ||
            !CanRotatePlacement())
            return;

        if (_selectedBuilding != null || _moveSourceCoord.HasValue)
            _mouseSelectController.RotatePreview();
    }

    public void SelectBuilding(Building prefab)
    {
        if (prefab == null)
            return;

        bool changed = TryChangeBuildingToPlace(prefab);
        Deselect();
        CancelMove();
        _mouseSelectController.BeginPlacementPreview(prefab);
        _mouseSelectController.SetPlacementActive(true);
        Debug.Log($"[BuildingPlacementController] 선택된 건물: {prefab.name}");

        if (changed)
        {
            BuildingToPlaceChanged.Invoke(prefab);
        }
    }

    // BabyDragonTower처럼 프리팹 자체엔 스프라이트가 없고 배치 시점에야 데이터로 정해지는
    // 건물의 고스트 미리보기 색을 지정할 때 쓴다. SelectBuilding 호출 직후에 불러야 한다 -
    // SelectBuilding이 내부적으로 새 대상을 잡으며 이전 오버라이드를 해제하기 때문이다.
    public void SetGhostSpriteOverride(Sprite sprite) => _mouseSelectController.SetGhostSpriteOverride(sprite);

    public void CancelBuildMode()
    {
        bool changed = TryChangeBuildingToPlace(null);
        _mouseSelectController.SetPlacementActive(false);

        if (changed)
        {
            BuildingToPlaceChanged.Invoke(null);
        }
    }

    // 값만 바꾸고 알리지 않는다 - 알림은 미리보기 세팅이 끝난 뒤 호출부가 보낸다.
    // 구독자가 절반만 준비된 상태를 보면 안 되기 때문이다.
    // CancelBuildMode는 이미 아무것도 고르지 않은 상태에서도 여러 경로에서 불리므로 바뀐 경우만 true를 준다.
    private bool TryChangeBuildingToPlace(Building prefab)
    {
        if (_selectedBuilding == prefab)
        {
            return false;
        }

        _selectedBuilding = prefab;
        return true;
    }

    public void RemoveSelectedBuilding()
    {
        if (!_selectedExistingBuildingCoord.HasValue)
            return;

        if (!IsDayForBuildActions)
            return;

        Building building = _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);
        bool removed = _gridMap.RemoveBuilding(_selectedExistingBuildingCoord.Value);

        if (building != null)
        {
            building.SetHighlighted(false, default);

            if (removed)
                RefundBuildCost(building);
        }

        _selectedExistingBuildingCoord = null;
        _mouseSelectController.ClearHighlights();
        _rangeIndicator?.Hide();
        _buffRangeIndicator?.Hide();

        NotifySelectedBuildingChanged();
    }

    // 건설 비용을 환급한다 - 낮밤 사이클이 한 번도 돌지 않은 당일 건설/철거는 전액, 그 외엔 DEMOLISH_REFUND_RATIO_LATE만큼.
    private void RefundBuildCost(Building building)
    {
        if (_resourceManager == null)
            return;

        bool isSameDay = _cycleManager != null && building.ConstructedCycle == _cycleManager.CurrentCycleNumber;
        float refundRatio = isSameDay ? DEMOLISH_REFUND_RATIO_SAME_DAY : DEMOLISH_REFUND_RATIO_LATE;

        IReadOnlyList<ResourceAmount> cost = building.BuildCost;
        var refund = new ResourceAmount[cost.Count];
        for (int i = 0; i < cost.Count; i++)
        {
            refund[i] = new ResourceAmount
            {
                Type = cost[i].Type,
                Amount = Mathf.RoundToInt(cost[i].Amount * refundRatio),
            };
        }

        _resourceManager.Add(refund);
    }

    public void EnterMoveMode()
    {
        if (!_selectedExistingBuildingCoord.HasValue)
            return;

        Building building = _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);
        if (building == null)
            return;

        if (!CanMoveNow(building))
            return;

        CancelBuildMode();
        _moveSourceCoord = _selectedExistingBuildingCoord;
        _selectedExistingBuildingCoord = null;
        NotifySelectedBuildingChanged();

        _mouseSelectController.BeginRepositionPreview(building);
        _mouseSelectController.SetPlacementActive(true);
    }

    public void CancelMove()
    {
        if (!_moveSourceCoord.HasValue)
            return;

        Building building = _gridMap.GetBuildingAt(_moveSourceCoord.Value);
        if (building != null)
            building.SetHighlighted(false, default);

        _moveSourceCoord = null;
        _mouseSelectController.SetPlacementActive(false);
        _mouseSelectController.ClearHighlights();
    }

    public void Deselect()
    {
        ClearSelectionWithoutNotify();
        NotifySelectedBuildingChanged();
    }

    // 알림 없이 선택만 걷는다. 곧바로 다른 건물을 고르는 경로(SelectExistingBuildingAt)가
    // 중간 상태까지 알리면, 선택 해제를 기다리던 쪽이 건물을 바꿔 클릭한 것만으로 넘어가버린다.
    private void ClearSelectionWithoutNotify()
    {
        if (_selectedExistingBuildingCoord.HasValue)
        {
            Building previous = _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);
            if (previous != null)
                previous.SetHighlighted(false, default);
        }

        _selectedExistingBuildingCoord = null;
        _mouseSelectController.ClearHighlights();
        _rangeIndicator?.Hide();
        _buffRangeIndicator?.Hide();
    }

    // 값을 따로 들고 다니지 않고 매번 현재 상태에서 계산한다 - 선택이 풀리는 경로가 여러 개라
    // 각자 알림을 맞춰 넣으면 어긋난다. 실제로 바뀐 경우만 알린다.
    private void NotifySelectedBuildingChanged()
    {
        Building current = _selectedExistingBuildingCoord.HasValue
            ? _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value)
            : null;

        if (ReferenceEquals(current, _notifiedSelection))
        {
            return;
        }

        _notifiedSelection = current;
        SelectedBuildingChanged.Invoke(current);
    }

    // _cancelMoveAction(우클릭)으로 새 건물 배치 미리보기, 기존 건물 이동 미리보기, 기존 건물 선택 하이라이트를 모두 취소한다.
    private void HandleCancelInput()
    {
        if (_cancelMoveAction == null ||
            !_cancelMoveAction.action.WasPerformedThisFrame() ||
            !CanCancelPlacement())
            return;

        if (_selectedBuilding != null)
            CancelBuildMode();
        else if (_moveSourceCoord.HasValue)
            CancelMove();
        else if (_selectedExistingBuildingCoord.HasValue)
            Deselect();
    }

    // 타워 위에서 클릭을 일정 시간 유지하면(롱프레스) 이동 모드로 진입한다.
    // (짧게 누르고 떼면 기존 클릭-선택 동작으로 처리되므로 서로 방해하지 않는다.)
    private void HandleLongPressMove()
    {
        if (_placeAction == null)
            return;

        if (!CanUseLongPressMove())
        {
            _holdCoord = null;
            return;
        }

        // 새 누름이 시작되면 직전 누름의 소비 표시를 지운다. 아래 조기 반환보다 앞이어야
        // 배치 미리보기 중에 시작된 누름도 정상적으로 초기화된다.
        if (_placeAction.action.WasPressedThisFrame())
            _longPressConsumedPress = false;

        // 이미 배치/이동 중이면 롱프레스를 추적하지 않는다.
        if (_selectedBuilding != null || _moveSourceCoord.HasValue)
        {
            _holdCoord = null;
            return;
        }

        if (_placeAction.action.WasReleasedThisFrame())
        {
            _holdCoord = null;
            return;
        }

        // 누르기 시작: 포인터 아래의 이동 가능한 건물을 홀드 대상으로 잡는다.
        if (_placeAction.action.WasPressedThisFrame())
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector3Int cell = _mouseSelectController.GetHoveredCell();
            Building building = _gridMap.GetBuildingAt(cell);

            if (!overUI && building != null && CanMoveNow(building))
            {
                _holdCoord = cell;
                _holdTimer = 0f;
            }
            else
            {
                _holdCoord = null;
            }

            return;
        }

        if (!_placeAction.action.IsPressed() || !_holdCoord.HasValue)
            return;

        // 누르는 동안 포인터가 같은 건물 위에 있어야 홀드가 유지된다.
        Building holdBuilding = _gridMap.GetBuildingAt(_holdCoord.Value);
        Building currentBuilding = _gridMap.GetBuildingAt(_mouseSelectController.GetHoveredCell());
        if (holdBuilding == null || currentBuilding != holdBuilding)
        {
            _holdCoord = null;
            return;
        }

        // 입력 홀드 타이머는 게임 속도(일시정지/배속)와 무관하게 항상 같은 체감으로 동작해야 한다.
        _holdTimer += Time.unscaledDeltaTime;
        if (_holdTimer >= _moveHoldDuration)
        {
            // 홀드 완료 → 해당 건물을 선택하고 이동 모드로 진입한다.
            SelectExistingBuildingAt(_holdCoord.Value);
            EnterMoveMode();
            _holdCoord = null;

            // 이 누름은 여기서 소비했다 - 손을 뗄 때 배치 확정까지 일어나면 이동이 곧바로 확정된다.
            _longPressConsumedPress = true;
        }
    }

    // 판정은 뗄 때 한다(점령·인구 모드와 같은 형태) - 누를 때 확정하면 좌드래그 카메라 패닝이
    // 항상 배치를 먼저 확정시키고, 같은 누름을 롱프레스가 이중으로 처리한다.
    private void HandlePlacementInput()
    {
        if (_placeAction == null)
            return;

        // 누른 순간의 포인터 위치와, 그 누름이 이 컨트롤러 것인지를 기록해 둔다.
        if (_placeAction.action.WasPressedThisFrame())
        {
            _pressScreenPosition = PointerScreenPosition();
            _isPressValid = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }

        if (!_placeAction.action.WasReleasedThisFrame())
            return;

        bool wasPressValid = _isPressValid;
        _isPressValid = false;

        // 롱프레스가 이미 이동 모드 진입으로 소비한 누름은 확정하지 않는다.
        if (!wasPressValid || _longPressConsumedPress)
            return;

        // 누른 지점에서 임계값 이상 움직였으면 클릭이 아니라 카메라 패닝으로 보고 무시한다.
        if (Vector2.Distance(_pressScreenPosition, PointerScreenPosition()) > _dragThreshold)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        ConfirmAtPointer();
    }

    private static Vector2 PointerScreenPosition() =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    private bool CanCancelPlacement()
    {
        foreach (IBuildModeInteractionQuery query in _interactionQueries)
        {
            if (!query.CanCancelPlacement())
            {
                return false;
            }
        }

        return true;
    }

    private bool CanRotatePlacement()
    {
        foreach (IBuildModeInteractionQuery query in _interactionQueries)
        {
            if (!query.CanRotatePlacement())
            {
                return false;
            }
        }

        return true;
    }

    private bool CanUseLongPressMove()
    {
        foreach (IBuildModeInteractionQuery query in _interactionQueries)
        {
            if (!query.CanUseLongPressMove())
            {
                return false;
            }
        }

        return true;
    }

    public void ConfirmAtPointer()
    {
        if (_selectedBuilding != null)
        {
            TryConstructAt(_mouseSelectController.CurrentAnchor);
            return;
        }

        if (_moveSourceCoord.HasValue)
        {
            Building building = _gridMap.GetBuildingAt(_moveSourceCoord.Value);
            if (building != null)
                TryMoveSelectedTo(_mouseSelectController.GetHoveredAnchor(_mouseSelectController.CurrentFootprintShape));

            return;
        }

        SelectExistingBuildingAt(ResolveClickedCell());
    }

    // 성은 그리드 정중앙에 3x3만 점유하는데(Castle.RegisterCenterFootprint) 스프라이트는 그보다
    // 훨씬 높게 그려져 있어, 탑 몸통을 눌러도 셀 판정으로는 빈 땅이 나온다. 그 경우에 한해 성
    // 스프라이트 안인지 한 번 더 보고 성의 셀로 돌린다.
    // 셀에 건물이 있으면 그대로 두므로, 성 앞을 가리는 타워를 못 고르게 되는 일은 없다.
    private Vector3Int ResolveClickedCell()
    {
        Vector3Int hoveredCell = _mouseSelectController.GetHoveredCell();

        if (_gridMap.GetBuildingAt(hoveredCell) != null)
            return hoveredCell;

        Castle castle = _gridMap.FindBuilding<Castle>(out Vector3Int castleCoord);

        if (castle == null || !castle.ContainsWorldPoint(_mouseSelectController.GetPointerWorldPoint()))
            return hoveredCell;

        return castleCoord;
    }

    public bool TryConstructAt(Vector3Int anchor)
    {
        if (_selectedBuilding == null)
            return false;

        if (_selectedBuilding is ResearchLab && _gridMap.HasBuilding<ResearchLab>())
            return false;

        // 건설은 낮에만 가능하다(기획 변경 - 새끼용 밤 배치 금지 요청을 계기로 전체 건물로 확장).
        if (!IsDayForBuildActions)
            return false;

        // 풋프린트를 한 번만 계산해 판정과 경고가 같은 칸 목록을 보게 한다(MouseSelectController.Update와 같은 형태).
        List<Vector3Int> footprint =
            _gridMap.GetFootprintCoords(anchor, _mouseSelectController.CurrentFootprintShape);

        if (!_gridMap.CanConstructBuildingFootprint(footprint, _selectedBuilding, null))
        {
            WarnIfBlockedByIceUnlockableTerrain(footprint);
            return false;
        }

        IReadOnlyList<ResourceAmount> cost = _selectedBuilding.BuildCost;
        if (_resourceManager != null && !_resourceManager.CanAfford(cost))
            return false;

        Debug.Log($"[BuildingPlacementController] 건설 위치: {anchor}");
        _gridMap.ConstructBuilding(_selectedBuilding, anchor, _mouseSelectController.PreviewRotationSteps);

        if (_cycleManager != null)
            _gridMap.GetBuildingAt(anchor)?.SetConstructedCycle(_cycleManager.CurrentCycleNumber);

        if (_resourceManager != null)
            _resourceManager.Spend(cost);

        SoundManager.Play(SoundId.BuildPlace);

        CancelBuildMode();
        return true;
    }

    // 풋프린트 중 "얼음 새끼용이라면 풀 수 있었을 지형"(용암) 때문에 막힌 칸이 있으면 그 이유를 알린다.
    // 호버 미리보기의 빨간 타일은 용암·절벽·물·미점령·점유를 전부 같은 색으로 보여줘서 "무엇을 하면
    // 되는지"를 알려주지 못한다. 반대로 절벽·물처럼 어떤 새끼용으로도 풀 수 없는 지형에서 이 문구가
    // 뜨면 안 되므로, 지형이 막은 경우(IsBlockedByTerrain)로 좁힌 뒤 지형 종류까지 확인한다.
    private void WarnIfBlockedByIceUnlockableTerrain(List<Vector3Int> footprint)
    {
        if (_warningWindow == null)
        {
            return;
        }

        foreach (Vector3Int coord in footprint)
        {
            if (_gridMap.IsBlockedByTerrain(coord) &&
                _gridMap.GetTerrainType(coord) == _iceUnlockableTerrain)
            {
                _warningWindow.ShowVolcanoConstructionWarning();
                return;
            }
        }
    }

    // 건물 종류별 건설 비용 데이터를 조회 - UI_BuildingSlot.ResolveName과 동일한 타입 분기 패턴.
    // 아직 비용이 정의되지 않은 건물(예: Tower)은 빈 배열을 돌려줘 비용 없이 취급된다.
    private static IReadOnlyList<ResourceAmount> ResolveBuildCost(Building building)
    {
        if (building is Factory factory && factory.Data != null)
            return factory.Data.BuildCost;

        if (building is ResearchLab researchLab && researchLab.Data != null)
            return researchLab.Data.BuildCost;

        return System.Array.Empty<ResourceAmount>();
    }

    public bool TryMoveSelectedTo(Vector3Int anchor)
    {
        if (!_moveSourceCoord.HasValue)
            return false;

        Vector3Int prevCoord = _moveSourceCoord.Value;
        Building building = _gridMap.GetBuildingAt(prevCoord);
        if (building == null)
            return false;

        // 낮에 이동 모드로 들어간 뒤 밤이 되어도 확정은 막는다 - 진입 시점(EnterMoveMode)에만
        // 검사하면 모드를 켠 채 밤을 맞아 이동을 확정할 수 있다. TryConstructAt과 같은 기준.
        if (!CanMoveNow(building))
            return false;

        if (!_gridMap.MoveBuilding(prevCoord, anchor, _mouseSelectController.PreviewRotationSteps))
        {
            // 플레이어 입장에선 새로 짓는 것과 옮기는 것이 똑같이 "용암에 못 놓는다"이므로 같은 안내를 준다.
            WarnIfBlockedByIceUnlockableTerrain(
                _gridMap.GetFootprintCoords(anchor, _mouseSelectController.CurrentFootprintShape));
            return false;
        }

        building.NotifyMoved();
        building.SetHighlighted(false, default);
        _moveSourceCoord = null;
        _mouseSelectController.SetPlacementActive(false);
        _mouseSelectController.ClearHighlights();
        return true;
    }

    public void SelectExistingBuildingAt(Vector3Int coord)
    {
        Building building = _gridMap.GetBuildingAt(coord);

        CancelBuildMode();
        CancelMove();
        ClearSelectionWithoutNotify();

        if (building != null)
        {
            _selectedExistingBuildingCoord = coord;
            _mouseSelectController.HighlightSelection(_gridMap.GetOccupiedCoords(coord));
            building.SetHighlighted(true, _mouseSelectController.SelectionHighlightColor);
            ShowRangeIndicatorFor(building);
        }

        // 최종 상태로 한 번만 알린다. A를 고른 뒤 B를 고르면 SelectedBuildingChanged(B) 하나만 나간다.
        NotifySelectedBuildingChanged();

        // 이미 선택된 건물을 다시 눌러도 매번 발화한다. SelectedBuilding은 파생 getter라
        // 폴링으로는 재클릭을 관측할 수 없다 - 위에서 선택을 걷은 뒤 같은 프레임에 다시 선택되므로
        // 구독자 입장에선 값이 바뀐 적이 없는 것으로 보인다.
        // 선택 해제(building == null)도 알려야 하므로 early return 하지 않는다.
        BuildingSelected.Invoke(building);
    }

    private void ShowRangeIndicatorFor(Building building)
    {
        ShowAttackRangeIndicatorFor(building);
        ShowBuffRangeIndicatorFor(building);
    }

    // 선택한 건물이 공격 가능한 타워면 실제 판정(TowerAttack.IsWithinAttackRange)과 같은 타원으로 사거리를 표시한다.
    private void ShowAttackRangeIndicatorFor(Building building)
    {
        if (!WiringGuard.Require(_rangeIndicator, nameof(_rangeIndicator), this))
        {
            return;
        }

        if (!(building is Tower tower) ||
            tower.Data == null ||
            !tower.Data.CanAttack ||
            tower.Attack == null)
        {
            _rangeIndicator.Hide();
            return;
        }

        _rangeIndicator.SetCenter(tower.transform.position);
        _rangeIndicator.Show(tower.Attack.EffectiveRange, tower.Attack.EffectiveRange * IsometricMath.RADIUS_Y_RATIO);
    }

    // 오라 타워는 현재 유효 반경을, 기존 새끼용은 BabyDragonBuffSystem과 같은 BuffRadius를 표시한다.
    private void ShowBuffRangeIndicatorFor(Building building)
    {
        if (!WiringGuard.Require(_buffRangeIndicator, nameof(_buffRangeIndicator), this))
        {
            return;
        }

        if (building is Tower tower &&
            tower.Data is ITowerAuraDataProvider provider &&
            provider.HasTowerAura)
        {
            if (TowerAuraSystem.TryGetActiveAura(
                tower,
                out _,
                out float auraRadius))
            {
                ShowBuffRange(tower.transform.position, auraRadius);
            }
            else
            {
                _buffRangeIndicator.Hide();
            }

            return;
        }

        if (building is BabyDragonTower babyDragon &&
            babyDragon.DragonData != null &&
            babyDragon.DragonData.BuffRadius > 0f)
        {
            ShowBuffRange(
                babyDragon.transform.position,
                babyDragon.DragonData.BuffRadius);
            return;
        }

        _buffRangeIndicator.Hide();
    }

    private void RefreshSelectedRangeIndicator()
    {
        if (!_selectedExistingBuildingCoord.HasValue)
        {
            return;
        }

        Building building =
            _gridMap.GetBuildingAt(_selectedExistingBuildingCoord.Value);

        if (building != null)
        {
            ShowRangeIndicatorFor(building);
        }
    }

    private void ShowBuffRange(Vector3 center, float radiusX)
    {
        float radiusY =
            radiusX * IsometricMath.RADIUS_Y_RATIO;

        _buffRangeIndicator.SetCenter(center);
        _buffRangeIndicator.Show(radiusX, radiusY);
    }

    public void CancelAll()
    {
        CancelBuildMode();
        CancelMove();
        Deselect();
    }

    // 건설 모드 진입 시 이미 배치된 건물의 타일을 표시 - 어떤 땅이 비어있는지 시각적으로 확인 가능
    public void ShowOccupiedTiles()
    {
        _isOccupiedOverlayVisible = true;
        RefreshOccupiedOverlay();
    }

    public void HideOccupiedTiles()
    {
        _isOccupiedOverlayVisible = false;
        _mouseSelectController.ClearOccupiedOverlay();
    }

    // 건설 모드가 열려있는 동안 건물 배치/철거/이동에 맞춰 표시된 타일을 최신 상태로 갱신
    private void RefreshOccupiedOverlay()
    {
        if (!_isOccupiedOverlayVisible)
            return;

        _mouseSelectController.ShowOccupiedOverlay(_gridMap.GetAllOccupiedCoords());
    }

}
