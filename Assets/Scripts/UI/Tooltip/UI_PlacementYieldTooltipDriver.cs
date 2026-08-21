using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 생산시설을 배치·이동하는 동안, 지금 커서가 가리키는 자리에 지으면 하루에 얼마가 나오는지를
/// 커서 옆 툴팁으로 보여준다.
///
/// 이 게임의 생산량은 데이터 고정값이 아니라 땅에서 나온다(셀 생산량 × 청크 배율 × 지형 페널티).
/// 그런데 배치 화면이 주는 정보는 초록/빨강 칸뿐이라, 같은 논이라도 몇 배씩 차이 나는 자리를
/// 플레이어가 구분할 방법이 없었다.
///
/// 표시기(UI_TooltipPresenter)는 건물 호버 툴팁과 공유한다 - owner 토큰으로 소유권을 가리므로
/// 서로의 툴팁을 지우지 않고, 저쪽은 배치 중에 스스로 물러난다(UI_BuildingTooltipDriver.IsPlacing).
/// </summary>
public class UI_PlacementYieldTooltipDriver : MonoBehaviour
{
    [SerializeField] private UI_TooltipPresenter _presenter;

    [Tooltip("무엇을 배치·이동하는 중인지 알려주는 컨트롤러.")]
    [SerializeField] private BuildingPlacementController _placementController;

    [Tooltip("고스트가 지금 가리키는 앵커·풋프린트 모양·배치 가능 여부의 출처.")]
    [SerializeField] private MouseSelectController _mouseSelectController;

    [SerializeField] private GridMap _gridMap;

    [Tooltip("자원 이름을 조회할 카탈로그(UI_FactoryInfoPopup과 같은 출처).")]
    [SerializeField] private ResourceCatalog _resourceCatalog;

    [Tooltip("지형 효과의 이름 문구 출처. 배치된 건물의 표식과 같은 테이블을 쓴다.")]
    [SerializeField] private BuildingEffectIconData _effectIconData;

    [Tooltip("지형 페널티 조회. 미연결이면 페널티 없이 계산한다 - 지형 페널티를 일부러 쓰지 않는 씬(튜토리얼)이 그렇다.")]
    [WiringOptional]
    [SerializeField] private TerrainPenaltySystem _terrainPenaltySystem;

    [SerializeField] private Color _gainColor = new Color(0.45f, 0.85f, 0.45f);
    [SerializeField] private Color _lossColor = new Color(0.9f, 0.45f, 0.45f);

    [Tooltip("각주(만충 기준 안내) 색. 본문보다 눈에 덜 띄어야 한다.")]
    [SerializeField] private Color _noteColor = new Color(0.65f, 0.65f, 0.65f);

    private readonly PlacementYieldEstimator _estimator = new();

    // 지금 툴팁을 띄운 근거. 이 셋 중 하나라도 바뀌어야 문자열을 다시 만든다 - 커서를 같은 칸 안에서
    // 움직이는 동안 매 프레임 재계산하면 배치 조작 내내 할당이 돈다.
    private Factory _shownFactory;
    private Vector3Int _shownAnchor;
    private int _shownRotationSteps;

    // 표시 여부는 _shownFactory 참조로 판정하지 않는다 - Unity의 == 오버로드 때문에 파괴된 건물이
    // null로 보여, 닫아야 할 순간에 Hide가 조기 반환하고 옛 내용이 화면에 남는다
    // (UI_BuildingTooltipDriver와 같은 이유).
    private bool _isShowing;

    private void OnDisable() => Hide();

    private void Update()
    {
        if (!TryResolveTarget(out Factory factory))
        {
            Hide();
            return;
        }

        Vector3Int anchor = _mouseSelectController.CurrentAnchor;
        int rotationSteps = _mouseSelectController.PreviewRotationSteps;

        if (_isShowing &&
            ReferenceEquals(factory, _shownFactory) &&
            anchor == _shownAnchor &&
            rotationSteps == _shownRotationSteps)
        {
            _presenter.MoveTo(PointerScreenPosition);
            return;
        }

        Show(factory, anchor, rotationSteps);
    }

    // 툴팁을 띄우면 안 되는 상황을 먼저 걸러낸다.
    private bool TryResolveTarget(out Factory factory)
    {
        factory = null;

        if (_placementController == null || _mouseSelectController == null || IsPointerOverUI)
        {
            return false;
        }

        // 못 놓는 자리에서 생산량을 띄우면 빨간 고스트와 서로 다른 말을 한다.
        // 왜 못 놓는지는 경고 토스트(UI_WarningWindow)가 따로 알린다.
        if (!_mouseSelectController.CanConstruct)
        {
            return false;
        }

        // 이동 중에는 BuildingToPlace가 비어 있다 - EnterMoveMode가 CancelBuildMode로 그 값을 지우고
        // 이동 원본만 세우기 때문이다(UI_BuildingTooltipDriver.IsPlacing 주석과 같은 사정).
        Building target = _placementController.BuildingToPlace;

        if (target == null && _placementController.IsMoving)
        {
            target = _placementController.SelectedBuilding;
        }

        factory = target as Factory;

        return factory != null;
    }

    private void Show(Factory factory, Vector3Int anchor, int rotationSteps)
    {
        // 카탈로그·아이콘 테이블이 비면 이름 없는 줄("  +14")이 나가므로 여기서 함께 막는다.
        if (!WiringGuard.Require(_presenter, nameof(_presenter), this) ||
            !WiringGuard.Require(_gridMap, nameof(_gridMap), this) ||
            !WiringGuard.Require(_resourceCatalog, nameof(_resourceCatalog), this) ||
            !WiringGuard.Require(_effectIconData, nameof(_effectIconData), this))
        {
            return;
        }

        // 커서가 다른 칸으로 넘어갔을 때만 도는 경로다(같은 칸 안의 움직임은 위에서 MoveTo로 빠진다).
        List<Vector3Int> footprint =
            _gridMap.GetFootprintCoords(anchor, _mouseSelectController.CurrentFootprintShape);

        if (!_estimator.TryEstimate(
                factory,
                _gridMap,
                _terrainPenaltySystem,
                footprint,
                _mouseSelectController.PreviewWorldPosition))
        {
            Hide();
            return;
        }

        TooltipContent content = PlacementYieldTooltipBuilder.Build(
            _estimator,
            _resourceCatalog,
            _effectIconData,
            _gainColor,
            _lossColor,
            _noteColor);

        if (!content.HasContent)
        {
            Hide();
            return;
        }

        _shownFactory = factory;
        _shownAnchor = anchor;
        _shownRotationSteps = rotationSteps;
        _isShowing = true;

        _presenter.Show(this, content, PointerScreenPosition);
    }

    // 남의 툴팁은 건드리지 않는다 - 표시기가 owner를 대조해 내가 띄운 것만 닫는다.
    private void Hide()
    {
        if (!_isShowing)
        {
            return;
        }

        _isShowing = false;
        _shownFactory = null;
        _presenter?.Hide(this);
    }

    private static Vector2 PointerScreenPosition =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    private static bool IsPointerOverUI =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
}
