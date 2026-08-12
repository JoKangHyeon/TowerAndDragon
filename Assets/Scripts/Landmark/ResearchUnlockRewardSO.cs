using UnityEngine;

// 타워 원형 보상. 실제로 지급되는 물건은 없다 - "이 랜드마크를 점령했다"는 사실 자체가
// 역설계 연구 노드의 해금 조건이 되고, 그 판정은 ResearchManager가 LandmarkManager에
// 직접 질의해서 처리한다(ILandmarkOwnershipQuery).
//
// 그럼에도 보상 목록에 항목으로 넣어두는 이유는 점령 패널에서 "무엇을 얻는지"를
// 다른 보상과 같은 방식으로 보여주기 위해서다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Landmark/Rewards/Research Unlock",
    fileName = "LandmarkResearchUnlockReward")]
public sealed class ResearchUnlockRewardSO : LandmarkRewardSO
{
    // 지급물이 없으므로 항상 성공으로 취급한다. 수령 원장에는 기록되어야 하므로 true다.
    public override bool Grant(LandmarkGrantContext context) => true;
}
