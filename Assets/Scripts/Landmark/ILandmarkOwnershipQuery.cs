// 랜드마크 보유 여부 조회 접점. 연구 해금 조건(ResearchManager.GetNodeState)이 이걸 읽는다.
// ResearchManager가 LandmarkManager를 직접 참조하지 않게 해서, 랜드마크가 없는 씬
// (테스트·튜토리얼)에서도 기존 연구가 그대로 돌아가게 한다.
//
// 구현체가 배선되지 않았을 때 "조건이 걸린 노드"는 잠긴 것으로 취급된다(fail-closed).
// 자세한 근거는 ResearchManager.LandmarkOwnershipQuery 주석 참고.
public interface ILandmarkOwnershipQuery
{
    bool IsClaimed(string landmarkId);
}
