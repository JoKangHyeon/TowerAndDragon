using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 커서를 올린 건물의 아웃라인을 켠다(BuildingHoverOutline이 붙은 건물만).
/// 한 번에 하나만 켜지므로, 겹쳐 있으면 화면 앞쪽이 이긴다.
///
/// 지면 셀이 아니라 스프라이트 몸통으로 판정하는 이유: 아웃라인은 "이건 눌러서 열 수 있다"는
/// 신호라 클릭 판정(BuildingClickCycle)과 같은 곳에서 떠야 한다. 셀 기준으로 두면 스프라이트가
/// 자기 풋프린트보다 높게 그려진 건물의 몸통 위에서 아웃라인이 꺼져 신호가 어긋난다.
/// (툴팁은 여전히 셀 기준이다 - GridCellHoveredBuildingSource의 설명 참고.)
///
/// 폴링으로 커서를 보는 이유는 UI_BuildingTooltipDriver와 같다 - Physics2DRaycaster를 붙이면
/// EventSystem.IsPointerOverGameObject()가 월드 콜라이더까지 "UI 위"로 판정해 건물 조작이 막힌다.
/// </summary>
public sealed class BuildingHoverOutlineDriver : MonoBehaviour
{
    [Tooltip("포인터의 월드 좌표를 얻을 컨트롤러.")]
    [SerializeField] private MouseSelectController _mouseSelectController;

    [Tooltip("배치·이동 미리보기 중에는 아웃라인을 띄우지 않는다. 비우면 그 판정을 건너뛴다.")]
    [WiringOptional]
    [SerializeField] private BuildingPlacementController _placementController;

    private BuildingHoverOutline _outlinedTarget;

    private void OnDisable() => ClearOutline();

    private void Update()
    {
        BuildingHoverOutline target = ResolveTarget();

        // 같은 대상이면 손대지 않는다 - 커서가 머무는 대부분의 프레임은 여기서 끝난다.
        if (ReferenceEquals(target, _outlinedTarget))
        {
            return;
        }

        ClearOutline();

        if (target == null)
        {
            return;
        }

        target.SetOutlined(true);
        _outlinedTarget = target;
    }

    private BuildingHoverOutline ResolveTarget()
    {
        if (IsPointerOverUI || IsPlacing)
        {
            return null;
        }

        if (!WiringGuard.Require(_mouseSelectController, nameof(_mouseSelectController), this))
        {
            return null;
        }

        // 매 프레임 도는 경로라 예외를 던지지 않는 판을 쓴다 - 카메라나 마우스가 없는 순간에는
        // 조용히 "가리키는 것 없음"으로 처리한다.
        if (!_mouseSelectController.TryGetPointerWorldPoint(out Vector3 pointerWorldPoint))
        {
            return null;
        }

        // 등록된 대상만 인덱서로 훑는다 - 그리드 전체를 돌지도, 열거자를 할당하지도 않는다.
        IReadOnlyList<BuildingHoverOutline> targets = BuildingHoverOutline.ActiveTargets;
        BuildingHoverOutline best = null;

        for (int i = 0; i < targets.Count; i++)
        {
            BuildingHoverOutline candidate = targets[i];

            if (!candidate.ContainsWorldPoint(pointerWorldPoint))
            {
                continue;
            }

            // 클릭 순환과 같은 비교자다 - 음수면 candidate가 더 앞쪽이다.
            if (best == null || Building.CompareFrontToBack(candidate.Building, best.Building) < 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private void ClearOutline()
    {
        // Unity의 ==를 쓴다 - 그 사이 철거로 파괴된 대상은 null로 잡혀 그냥 넘어간다
        // (파괴 시 대상 자신의 OnDisable이 이미 아웃라인을 껐다).
        if (_outlinedTarget != null)
        {
            _outlinedTarget.SetOutlined(false);
        }

        _outlinedTarget = null;
    }

    private static bool IsPointerOverUI =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // 이동은 BuildingToPlace를 쓰지 않는다 - EnterMoveMode가 CancelBuildMode로 그 값을 비운 뒤
    // _moveSourceCoord만 세우므로, 배치만 보면 고스트를 끌고 다니는 동안 아웃라인이 함께 뜬다.
    private bool IsPlacing =>
        _placementController != null &&
        (_placementController.BuildingToPlace != null || _placementController.IsMoving);
}
