using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 커서를 올린 타워·건물·적의 아웃라인을 켠다. 한 번에 하나만 켜지므로, 겹쳐 있으면 화면 앞쪽이 이긴다.
///
/// 지면 셀이 아니라 스프라이트 몸통으로 건물을 판정하는 이유: 아웃라인은 "이건 눌러서 열 수 있다"는
/// 신호라 클릭 판정(BuildingClickCycle)과 같은 곳에서 떠야 한다. 셀 기준으로 두면 스프라이트가
/// 자기 풋프린트보다 높게 그려진 건물의 몸통 위에서 아웃라인이 꺼져 신호가 어긋난다.
/// (툴팁은 여전히 셀 기준이다 - GridCellHoveredBuildingSource의 설명 참고.) 적도 같은 이유로
/// 콜라이더가 아니라 몸통 우선으로 판정한다(MonsterPicker 참고).
///
/// 폴링으로 커서를 보는 이유는 UI_BuildingTooltipDriver와 같다 - Physics2DRaycaster를 붙이면
/// EventSystem.IsPointerOverGameObject()가 월드 콜라이더까지 "UI 위"로 판정해 건물 조작이 막힌다.
///
/// Update가 아니라 LateUpdate에서 판정하는 이유: 카메라 이동(CameraController.ApplyMovement)·적
/// 이동(GroundSplineMovement 등)·정렬 순서 갱신(IsometricDepthSorter)·좌우 반전
/// (MonsterSpriteFlipper)이 전부 Update에서 일어난다. MonoBehaviour 간 Update 순서는 보장되지
/// 않으므로 Update에서 판정하면 한 프레임 묵은 카메라 위치·적 위치·좌우 반전을 기준으로 판정하게
/// 된다 - 특히 flipX가 한 프레임 어긋나면 SpriteHitTest가 스프라이트 중심선을 기준으로 좌우가
/// 통째로 뒤집힌 판정을 내린다. LateUpdate는 이 프레임의 최종 상태에 대고 판정하므로 이 문제가
/// 없고, EPO는 그보다도 늦은 렌더 패스 등록 시점에 대상 목록을 읽으므로 반대 방향 순서 위험도 없다.
/// </summary>
public sealed class HoverOutlineDriver : MonoBehaviour
{
    private static readonly Color DEFAULT_TOWER_OUTLINE_COLOR = new(1f, 0.92156863f, 0.015686275f, 1f);
    private static readonly Color DEFAULT_BUILDING_OUTLINE_COLOR = new(0.016616056f, 0.7956049f, 1.0592737f, 1f);
    private static readonly Color DEFAULT_ENEMY_OUTLINE_COLOR = Color.red;

    [Tooltip("포인터의 월드 좌표를 얻을 컨트롤러. 건물·적 판정이 이 좌표 하나만 공유해야 서로 다른 것을 가리키지 않는다.")]
    [SerializeField] private MouseSelectController _mouseSelectController;

    [Tooltip("현재 배치된 건물 전체를 훑는 데 쓴다.")]
    [SerializeField] private GridMap _gridMap;

    [Tooltip("배치·이동 미리보기 중에는 아웃라인을 띄우지 않는다. 비우면 그 판정을 건너뛴다.")]
    [WiringOptional]
    [SerializeField] private BuildingPlacementController _placementController;

    [Tooltip("스킬 타겟팅 중에는 아웃라인을 띄우지 않는다 - 이미 자체 인디케이터(SkillTargetIndicator·" +
        "RangeIndicator)가 있고, 이 아웃라인은 스킬별 유효 타겟 레이어를 모르므로 지금 스킬로 못 맞히는" +
        " 적에 빨간 외곽선이 뜰 수 있다. 비우면 그 판정을 건너뛴다.")]
    [WiringOptional]
    [SerializeField] private SkillTargetingController _skillTargetingController;

    [SerializeField] private Color _towerOutlineColor = DEFAULT_TOWER_OUTLINE_COLOR;
    [SerializeField] private Color _buildingOutlineColor = DEFAULT_BUILDING_OUTLINE_COLOR;
    [SerializeField] private Color _enemyOutlineColor = DEFAULT_ENEMY_OUTLINE_COLOR;

    private GameObject _outlinedObject;

    // _outlinedObject 참조와 별개로 둔다 - 참조의 널 여부만으로 "내가 뭔가 켜 뒀나"를 판단하면,
    // 대상이 파괴됐을 때(Unity의 == 오버로드) 꺼야 할 순간에 조기 반환해 버린다. 적이 죽어 사라지는
    // 것은 예외가 아니라 정상 경로라 건물 철거보다 훨씬 자주 걸린다(UI_MonsterTooltipDriver의
    // _isShowing과 같은 이유).
    private bool _hasOutline;

    private void OnDisable() => ClearOutline();

    private void LateUpdate()
    {
        if (!TryResolveTarget(out GameObject target, out Color color))
        {
            ClearOutline();
            return;
        }

        // 같은 대상이면 손대지 않는다 - 커서가 머무는 대부분의 프레임은 여기서 끝난다.
        if (_hasOutline && ReferenceEquals(target, _outlinedObject))
        {
            return;
        }

        ClearOutline();

        HoverOutline.Show(target, color);
        _outlinedObject = target;
        _hasOutline = true;
    }

    private bool TryResolveTarget(out GameObject target, out Color color)
    {
        target = null;
        color = default;

        if (IsPointerOverUI || IsPlacing || IsTargetingSkill)
        {
            return false;
        }

        if (!WiringGuard.Require(_mouseSelectController, nameof(_mouseSelectController), this))
        {
            return false;
        }

        // 매 프레임 도는 경로라 예외를 던지지 않는 판을 쓴다 - 카메라나 마우스가 없는 순간에는
        // 조용히 "가리키는 것 없음"으로 처리한다.
        if (!_mouseSelectController.TryGetPointerWorldPoint(out Vector3 pointerWorldPoint))
        {
            return false;
        }

        BaseMonster hoveredMonster = ResolveHoveredMonster(pointerWorldPoint);
        Building hoveredBuilding = ResolveHoveredBuilding(pointerWorldPoint);

        // 교차 비교: 적·건물 모두 IsometricMath.ComputeDepthSortOrder를 눈금으로 쓰는 sortingOrder라
        // 값이 큰 쪽이 화면에서 실제로 위에 보인다. 동점이면 적을 우선한다 - 건물과 달리 적은 클릭
        // 선택 대상이 없어(스킬 타겟팅 밖에서는 클릭해도 아무 일도 없다) 아웃라인의 의미가 "지금
        // 툴팁이 이름을 대는 대상"에 가깝고, 동점 자체가 그리기 순서상 정의되지 않은 경계라
        // 어느 쪽을 선택해도 마찬가지로 참이다.
        if (hoveredMonster != null &&
            (hoveredBuilding == null || hoveredMonster.DepthSortOrder >= hoveredBuilding.DepthSortOrder))
        {
            target = hoveredMonster.gameObject;
            color = _enemyOutlineColor;
            return true;
        }

        if (hoveredBuilding != null)
        {
            target = hoveredBuilding.gameObject;
            color = hoveredBuilding is Tower ? _towerOutlineColor : _buildingOutlineColor;
            return true;
        }

        return false;
    }

    // 적이 하나도 없는 낮 시간대 전체를 판정 없이 건너뛴다.
    private static BaseMonster ResolveHoveredMonster(Vector3 pointerWorldPoint) =>
        BaseMonster.ActiveMonsters.Count == 0
            ? null
            : MonsterPicker.FindUnderPointer(pointerWorldPoint, LayerMasks.Enemy);

    // 클릭 후보(BuildingClickCycle)와 같은 집합·같은 비교자·같은 제외 규칙(IsClickSelectable)을 쓴다 -
    // "눌리는 것"과 "아웃라인이 뜨는 것"이 갈라지지 않는다. 그래서 성 위에서는 성이 아니라 성에
    // 가려진 뒤쪽 건물에 아웃라인이 뜨고, 클릭도 같은 건물을 잡는다.
    private Building ResolveHoveredBuilding(Vector3 pointerWorldPoint)
    {
        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return null;
        }

        Building best = null;

        foreach (Building candidate in _gridMap.Buildings)
        {
            if (!candidate.IsClickSelectable || !candidate.ContainsWorldPoint(pointerWorldPoint))
            {
                continue;
            }

            // 음수면 candidate가 더 앞쪽이다.
            if (best == null || Building.CompareFrontToBack(candidate, best) < 0)
            {
                best = candidate;
            }
        }

        return best;
    }

    private void ClearOutline()
    {
        if (_hasOutline)
        {
            HoverOutline.Hide(_outlinedObject);
        }

        _outlinedObject = null;
        _hasOutline = false;
    }

    private static bool IsPointerOverUI =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // 이동은 BuildingToPlace를 쓰지 않는다 - EnterMoveMode가 CancelBuildMode로 그 값을 비운 뒤
    // _moveSourceCoord만 세우므로, 배치만 보면 고스트를 끌고 다니는 동안 아웃라인이 함께 뜬다.
    private bool IsPlacing =>
        _placementController != null &&
        (_placementController.BuildingToPlace != null || _placementController.IsMoving);

    private bool IsTargetingSkill =>
        _skillTargetingController != null && _skillTargetingController.IsTargeting;
}
