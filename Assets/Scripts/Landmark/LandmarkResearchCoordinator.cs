using UnityEngine;

// ResearchManager.LandmarkOwnershipQuery 슬롯에 LandmarkManager를 배선한다.
// ChunkYieldCoordinator와 같은 관용구 - 슬롯 대입만 담당한다.
//
// 이 배선이 없으면 랜드마크 조건이 걸린 역설계 노드는 영원히 LandmarkLocked로 남는다
// (ResearchManager.IsRequiredLandmarkClaimed 참고).
public sealed class LandmarkResearchCoordinator : MonoBehaviour
{
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private LandmarkManager _landmarkManager;

    private void OnEnable()
    {
        if (_researchManager == null || _landmarkManager == null)
        {
            Debug.LogError(
                "[LandmarkResearchCoordinator] 참조 누락 - 역설계 연구가 영원히 잠긴 상태로 남습니다.",
                this);
            return;
        }

        _researchManager.LandmarkOwnershipQuery = _landmarkManager;
    }

    private void OnDisable()
    {
        if (_researchManager != null &&
            ReferenceEquals(_researchManager.LandmarkOwnershipQuery, _landmarkManager))
        {
            _researchManager.LandmarkOwnershipQuery = null;
        }
    }
}
