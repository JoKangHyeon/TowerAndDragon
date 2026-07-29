using UnityEngine;

// ConquestManager.ResearchModifierQuery 슬롯에 ResearchManager를 직접 대입하지 않고
// ConquestModifierComposite를 통해 배선한다 - 용 스킬트리(DragonModifierCoordinator)도
// 같은 슬롯에 기여해야 하므로 단일 소스 대입은 서로를 덮어쓴다.
public sealed class ConquestResearchCoordinator : MonoBehaviour
{
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private ConquestModifierComposite _conquestComposite;

    private void OnEnable()
    {
        if (_conquestManager == null || _conquestComposite == null)
        {
            Debug.LogError(
                "[ConquestResearchCoordinator] 참조 누락 - 연구 점령 할인·기간감소가 전혀 적용되지 않습니다.",
                this);
            return;
        }

        _conquestManager.ResearchModifierQuery = _conquestComposite;
        _conquestComposite.Register(_researchManager);
    }

    private void OnDisable()
    {
        if (_conquestComposite != null)
        {
            _conquestComposite.Unregister(_researchManager);
        }

        if (_conquestManager != null &&
            ReferenceEquals(_conquestManager.ResearchModifierQuery, _conquestComposite))
        {
            _conquestManager.ResearchModifierQuery = null;
        }
    }
}
