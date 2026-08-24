using UnityEngine;

// DragonTreeManager를 5개 Composite(타워 배율·청크 생산·시야·점령·타워 최대체력)에 등록한다.
// ConquestResearchCoordinator와 동일한 관용구(OnEnable 등록 / OnDisable 해제) -
// 소스가 0개인 Composite는 중립값(배율 1, 가산 0)을 반환하므로 등록 순서에 무관하게 안전하다.
// 시야 Composite에는 CastleVisionCoordinator도 같은 DragonTreeManager를 등록한다. Composite가
// 참조 카운트(RefCountedSourceSet)로 소유권을 세므로, 이쪽이 먼저 비활성화돼도 저쪽의 기여는
// 남는다 - 중복 등록을 지우는 대신 각 코디네이터가 독립적으로 필요를 선언하는 구조를 유지한다.
public sealed class DragonModifierCoordinator : MonoBehaviour
{
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private TowerStatMultiplierComposite _statComposite;
    [SerializeField] private ChunkYieldMultiplierComposite _yieldComposite;
    [SerializeField] private VisionRadiusComposite _visionComposite;
    [SerializeField] private ConquestModifierComposite _conquestComposite;

    [Tooltip("타워 최대체력 Composite. 비어 있으면 최대체력 패시브(생명 조율 등)가 적용되지 않는다.")]
    [SerializeField] private TowerMaxHealthMultiplierComposite _maxHealthComposite;

    private void OnEnable()
    {
        if (!WiringGuard.Require(_dragonTreeManager, nameof(_dragonTreeManager), this))
        {
            return;
        }

        _statComposite?.Register(_dragonTreeManager);
        _yieldComposite?.Register(_dragonTreeManager);
        _visionComposite?.Register(_dragonTreeManager);
        _conquestComposite?.Register(_dragonTreeManager);
        _maxHealthComposite?.Register(_dragonTreeManager);
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
        _maxHealthComposite?.Unregister(_dragonTreeManager);
    }
}
