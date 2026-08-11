using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 커서가 올라간 건물의 효과 설명을 툴팁으로 띄운다.
///
/// UI_TooltipTrigger(IPointerEnterHandler)를 쓰지 않고 월드 좌표로 직접 판정하는 이유:
/// 트리거를 쓰려면 표식에 GraphicRaycaster가 필요한데, 그러면 커서가 건물 위에 있는 동안
/// EventSystem.IsPointerOverGameObject가 참이 되어 건물 배치·인구 배치·카메라 드래그가 전부
/// 막힌다(BuildingPlacementController:449, WorkerModeController:268, CameraController:328 등이
/// 그 검사로 월드 입력을 차단한다).
/// </summary>
public class BuildingEffectHoverTooltip : MonoBehaviour
{
    [Tooltip("커서의 그리드 좌표를 계산하는 컨트롤러. 하이라이트와 같은 기준을 써야 표시 위치가 어긋나지 않는다.")]
    [SerializeField] private MouseSelectController _mouseSelectController;

    [SerializeField] private GridMap _gridMap;

    [Tooltip("툴팁을 그릴 표시기. HUD 캔버스의 것을 그대로 공유한다.")]
    [SerializeField] private UI_TooltipPresenter _tooltipPresenter;

    [Tooltip("효과 판정과 아이콘 테이블을 함께 쓰는 표식 시스템.")]
    [SerializeField] private BuildingEffectBadgeSystem _badgeSystem;

    private readonly List<BuildingEffectDescriptor> _effectBuffer = new();

    // 문구는 호버 대상이 바뀔 때만 새로 만든다 - 매 프레임 만들면 같은 문자열을 계속 새로 할당한다.
    private Building _hoveredBuilding;

    private void OnDisable()
    {
        ClearHover();
    }

    private void Update()
    {
        Building hovered = ResolveHoveredBuilding();

        if (!ReferenceEquals(hovered, _hoveredBuilding))
        {
            _hoveredBuilding = hovered;
            ShowForHovered();
            return;
        }

        if (_hoveredBuilding != null && Mouse.current != null)
        {
            _tooltipPresenter.MoveTo(Mouse.current.position.ReadValue());
        }
    }

    private Building ResolveHoveredBuilding()
    {
        if (_mouseSelectController == null ||
            _gridMap == null ||
            _tooltipPresenter == null ||
            _badgeSystem == null ||
            Mouse.current == null)
        {
            return null;
        }

        // HUD 위에서는 그 요소의 툴팁이 우선이다 - 창 뒤에 가린 건물의 설명이 겹쳐 뜨지 않게 한다.
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        return _gridMap.GetBuildingAt(_mouseSelectController.GetHoveredCell());
    }

    private void ShowForHovered()
    {
        if (_hoveredBuilding == null)
        {
            ClearHover();
            return;
        }

        if (!_badgeSystem.TryResolveEffects(
                _hoveredBuilding,
                _effectBuffer,
                out TerrainPenaltyModifiers modifiers))
        {
            _tooltipPresenter.Hide(this);
            return;
        }

        TooltipContent content = BuildingEffectTooltipBuilder.Build(
            _hoveredBuilding,
            _effectBuffer,
            modifiers,
            _badgeSystem.IconData);

        _tooltipPresenter.Show(this, content, Mouse.current.position.ReadValue());
    }

    private void ClearHover()
    {
        _hoveredBuilding = null;
        _tooltipPresenter?.Hide(this);
    }
}
