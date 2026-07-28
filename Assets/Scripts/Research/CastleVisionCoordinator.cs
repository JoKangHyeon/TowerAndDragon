using UnityEngine;

// 성 주변 시야 공개 - 연구·용 스킬트리 양쪽의 시야 보너스가 완료될 때마다 공개 반경을 갱신한다.
// 개별 매니저 값이 아니라 VisionRadiusComposite의 합산값을 써야 두 시스템의 기여가 합쳐진다.
public sealed class CastleVisionCoordinator : MonoBehaviour
{
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private VisionRadiusComposite _visionComposite;
    [SerializeField] private Castle _castle;

    private void OnEnable()
    {
        if (_visionComposite == null)
        {
            Debug.LogError(
                "[CastleVisionCoordinator] VisionRadiusComposite 참조가 없어 시야 보너스가 전혀 적용되지 않습니다.",
                this);
        }

        if (_researchManager != null)
        {
            _visionComposite?.Register(_researchManager);
            _researchManager.NodeCompleted.AddListener(HandleResearchNodeCompleted);
        }

        if (_dragonTreeManager != null)
        {
            _visionComposite?.Register(_dragonTreeManager);
            _dragonTreeManager.NodeUnlocked.AddListener(HandleDragonNodeUnlocked);
        }
    }

    private void OnDisable()
    {
        if (_researchManager != null)
        {
            _visionComposite?.Unregister(_researchManager);
            _researchManager.NodeCompleted.RemoveListener(HandleResearchNodeCompleted);
        }

        if (_dragonTreeManager != null)
        {
            _visionComposite?.Unregister(_dragonTreeManager);
            _dragonTreeManager.NodeUnlocked.RemoveListener(HandleDragonNodeUnlocked);
        }
    }

    private void HandleResearchNodeCompleted(ResearchNodeData node)
    {
        RevealWithCurrentBonus();
    }

    private void HandleDragonNodeUnlocked(ProgressionNodeData node)
    {
        RevealWithCurrentBonus();
    }

    private void RevealWithCurrentBonus()
    {
        if (_castle == null || _visionComposite == null)
        {
            return;
        }

        _castle.RevealSurroundingChunks(_visionComposite.GetVisionRadiusBonus());
    }
}
