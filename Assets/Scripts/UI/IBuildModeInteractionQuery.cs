using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 건설 창 안의 클릭을 지금 받아도 되는지 묻는다.
///
/// 화면 오버레이는 조준 대상이 있는 버튼을 막는 데는 충분하지만, 타일 배치처럼 월드를 눌러야 해서
/// 화면 전체를 덮을 수 없는 단계에서는 열린 건설 창의 다른 탭과 슬롯이 그대로 눌린다.
/// 튜토리얼 러너가 건설 창과 배치 컨트롤러에 이 질의를 등록해 현재 행동과 관계없는 입력만 막는다.
///
/// 질의(CanX)와 통보(NotifyX)를 나눠 둔다 - 미리보기 색을 칠하려고 매 프레임 폴링되는 질의 안에서
/// 사유를 말하면 커서를 올리기만 해도 문구가 뜬다. 사유는 실제로 누른 자리에서만 낸다.
/// </summary>
public interface IBuildModeInteractionQuery
{
    bool CanSelectFilter(RectTransform filterTab);
    bool CanSelectBuilding(Building prefab);
    bool CanPlaceBuildingAt(Building prefab, Vector3Int anchor);

    /// <summary>배치가 막힌 자리를 실제로 눌렀을 때 불린다. 자기가 막은 것이 맞을 때만 사유를 낸다.</summary>
    void NotifyPlacementBlocked(Building prefab, Vector3Int anchor);

    bool TryGetPlacementGuideAnchors(Building prefab, out IReadOnlyList<Vector3Int> anchors);
    bool CanMoveSelectedBuilding();
    bool CanRemoveSelectedBuilding();
    bool CanCancelPlacement();
    bool CanRotatePlacement();
    bool CanUseLongPressMove();
}
