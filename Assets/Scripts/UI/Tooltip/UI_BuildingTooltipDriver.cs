using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 그리드에 배치된 건물에 커서를 올리면 툴팁을 띄운다(누르지 않아도 된다).
///
/// UI_TooltipTrigger를 쓰지 못하는 이유: 그쪽은 EventSystem 포인터 이벤트(IPointerEnterHandler) 기반인데
/// Main Camera에 Physics2DRaycaster가 없어 월드 오브젝트에는 포인터 이벤트가 오지 않는다. 그렇다고
/// Raycaster를 붙이면 EventSystem.IsPointerOverGameObject()가 월드 콜라이더까지 "UI 위"로 판정해
/// 건물 배치·선택·카메라 드래그·스킬 타겟팅이 새끼용 위에서 한꺼번에 막힌다(그 판정을 쓰는 곳이 7군데다).
/// 그래서 SpriteHoverFade와 같은 폴링 방식으로 커서 위치를 직접 본다.
///
/// 표시기는 자원 툴팁과 공유해도 된다 - UI_TooltipPresenter가 owner 토큰으로 소유권을 가리므로
/// 서로의 툴팁을 지우지 않는다.
/// </summary>
public class UI_BuildingTooltipDriver : MonoBehaviour
{
    private const float DEFAULT_HOVER_DELAY = 0.3f;
    private const float DEFAULT_REFRESH_INTERVAL = 0.25f;

    [SerializeField] private UI_TooltipPresenter _presenter;

    [Tooltip("커서 아래 건물을 찾는 방법. 판정을 바꿀 때 이 칸만 갈아끼운다.")]
    [SerializeField] private HoveredBuildingSource _hoveredBuildingSource;

    [Tooltip("건물 종류별 툴팁 제공자. 위에서부터 물어보고 처음 응답한 것을 쓴다.")]
    [SerializeField] private BuildingTooltipProvider[] _providers;

    [Tooltip("배치·이동 미리보기 중에는 툴팁을 띄우지 않는다. 비우면 그 판정을 건너뛴다.")]
    [SerializeField] private BuildingPlacementController _placementController;

    [Tooltip("커서를 올린 뒤 툴팁이 뜨기까지 머물러야 하는 시간(초). 0이면 스쳐 지나가도 떠서 깜빡인다.")]
    [SerializeField] private float _hoverDelay = DEFAULT_HOVER_DELAY;

    [Tooltip("표시 중인 툴팁의 값을 다시 계산하는 주기(초). 생산량·먹이는 다른 조작으로 바뀔 수 있다.")]
    [SerializeField] private float _refreshInterval = DEFAULT_REFRESH_INTERVAL;

    // 커서가 지금 얹혀 있는 건물. 아직 지연 시간을 못 채웠으면 표시되지 않은 상태다.
    private Building _hoveredBuilding;

    // 실제로 툴팁을 띄운 건물. 내용을 다시 만들 때 쓴다.
    private Building _shownBuilding;

    // 표시 중인지는 건물 참조가 아니라 이 플래그로 판정한다 - 띄워 둔 건물이 철거되면
    // Unity의 == 오버로드 때문에 _shownBuilding != null이 false가 되어, 닫아야 할 순간에
    // Hide가 조기 반환하고 툴팁이 옛 내용 그대로 화면에 남는다.
    private bool _isShowing;

    private float _hoverElapsed;
    private float _refreshElapsed;

    private bool IsShowing => _isShowing;

    private void OnDisable()
    {
        Hide();
    }

    private void Update()
    {
        if (!TryResolveTarget(out Building building))
        {
            ClearHover();
            return;
        }

        if (!ReferenceEquals(building, _hoveredBuilding))
        {
            // 다른 건물로 옮겨갔다. 이전 툴팁을 즉시 걷고 지연을 처음부터 다시 센다.
            Hide();
            _hoveredBuilding = building;
            _hoverElapsed = 0f;
        }

        // 호버 판정은 게임 속도와 무관한 UI 피드백이라 unscaled로 센다(SpriteHoverFade와 같은 이유).
        _hoverElapsed += Time.unscaledDeltaTime;

        if (_hoverElapsed < _hoverDelay)
        {
            return;
        }

        if (IsShowing)
        {
            FollowCursor();
            RefreshPeriodically();
            return;
        }

        Show(building);
    }

    // 툴팁을 띄우면 안 되는 상황을 먼저 걸러낸다.
    private bool TryResolveTarget(out Building building)
    {
        building = null;

        if (_hoveredBuildingSource == null || IsPointerOverUI || IsPlacing)
        {
            return false;
        }

        return _hoveredBuildingSource.TryGetHoveredBuilding(out building);
    }

    private void Show(Building building)
    {
        if (!TryBuildContent(building, out TooltipContent content))
        {
            // 담당 제공자가 없는 건물이다. 지연을 다시 세지 않도록 호버 대상으로는 남겨 둔다.
            return;
        }

        if (!WiringGuard.Require(_presenter, nameof(_presenter), this))
        {
            return;
        }

        _shownBuilding = building;
        _isShowing = true;
        _refreshElapsed = 0f;
        _presenter.Show(this, content, PointerScreenPosition);
    }

    private void RefreshPeriodically()
    {
        _refreshElapsed += Time.unscaledDeltaTime;

        if (_refreshElapsed < _refreshInterval)
        {
            return;
        }

        _refreshElapsed = 0f;

        // 띄워 둔 건물이 철거됐거나 내용이 비면 닫는다. TryBuildContent는 파괴된 건물에
        // is 패턴이 실패해 false를 돌려주므로 철거도 이 경로로 걸린다.
        if (TryBuildContent(_shownBuilding, out TooltipContent content))
        {
            _presenter.Refresh(this, content);
            return;
        }

        Hide();
    }

    private bool TryBuildContent(Building building, out TooltipContent content)
    {
        content = default;

        if (building == null || _providers == null)
        {
            return false;
        }

        foreach (BuildingTooltipProvider provider in _providers)
        {
            if (provider != null && provider.TryBuild(building, out content))
            {
                return true;
            }
        }

        return false;
    }

    private void FollowCursor()
    {
        _presenter.MoveTo(PointerScreenPosition);
    }

    private void ClearHover()
    {
        Hide();
        _hoveredBuilding = null;
        _hoverElapsed = 0f;
    }

    // 남의 툴팁은 건드리지 않는다 - 표시기가 owner를 대조해 내가 띄운 것만 닫는다.
    private void Hide()
    {
        if (!IsShowing)
        {
            return;
        }

        _isShowing = false;
        _shownBuilding = null;
        _presenter?.Hide(this);
    }

    private static Vector2 PointerScreenPosition =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    private static bool IsPointerOverUI =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // 이동은 BuildingToPlace를 쓰지 않는다 - EnterMoveMode가 CancelBuildMode로 그 값을 비운 뒤
    // _moveSourceCoord만 세우므로, 배치만 보면 고스트를 끌고 다니는 동안 툴팁이 겹쳐 뜬다.
    private bool IsPlacing =>
        _placementController != null &&
        (_placementController.BuildingToPlace != null || _placementController.IsMoving);
}
