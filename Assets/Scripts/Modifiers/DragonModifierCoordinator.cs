using UnityEngine;

// DragonTreeManager를 4개 Composite(타워 배율·청크 생산·시야·점령)에 등록한다.
// ConquestResearchCoordinator와 동일한 관용구(OnEnable 등록 / OnDisable 해제) -
// 소스가 0개인 Composite는 중립값(배율 1, 가산 0)을 반환하므로 등록 순서에 무관하게 안전하다.
public sealed class DragonModifierCoordinator : MonoBehaviour
{
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private TowerStatMultiplierComposite _statComposite;
    [SerializeField] private ChunkYieldMultiplierComposite _yieldComposite;
    [SerializeField] private VisionRadiusComposite _visionComposite;
    [SerializeField] private ConquestModifierComposite _conquestComposite;

    private void OnEnable()
    {
        if (_dragonTreeManager == null)
        {
            return;
        }

        _statComposite?.Register(_dragonTreeManager);
        _yieldComposite?.Register(_dragonTreeManager);
        _visionComposite?.Register(_dragonTreeManager);
        _conquestComposite?.Register(_dragonTreeManager);
    }

    private void OnDisable()
    {
        if (_dragonTreeManager == null)
        {
            return;
        }

        _statComposite?.Unregister(_dragonTreeManager);
        _yieldComposite?.Unregister(_dragonTreeManager);
        _visionComposite?.Unregister(_dragonTreeManager);
        _conquestComposite?.Unregister(_dragonTreeManager);
    }
}
