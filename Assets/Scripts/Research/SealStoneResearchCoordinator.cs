using UnityEngine;

// PortalSealManager.UnlockQuery 슬롯에 ResearchManager를 배선한다.
// LandmarkResearchCoordinator와 같은 관용구 - 슬롯 대입만 담당한다.
//
// 이 배선이 없으면 UnlockQuery가 null로 남고, PortalSealManager는 그 경우를
// "해금됨"으로 접기 때문에 봉인석 연구를 하지 않아도 봉인석을 지을 수 있다.
public sealed class SealStoneResearchCoordinator : MonoBehaviour
{
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private PortalSealManager _portalSealManager;

    private void OnEnable()
    {
        if (_researchManager == null || _portalSealManager == null)
        {
            Debug.LogError(
                "[SealStoneResearchCoordinator] 참조 누락 - 봉인석이 연구 없이도 건설 가능한 상태로 남습니다.",
                this);
            return;
        }

        _portalSealManager.UnlockQuery = _researchManager;
    }

    private void OnDisable()
    {
        if (_portalSealManager != null &&
            ReferenceEquals(_portalSealManager.UnlockQuery, _researchManager))
        {
            _portalSealManager.UnlockQuery = null;
        }
    }
}
