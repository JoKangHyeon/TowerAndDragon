using UnityEngine;

// GridMap.YieldMultiplierQuery 슬롯에 ChunkYieldMultiplierComposite를 배선한다 - 슬롯 대입만
// 담당하고, Composite에 대한 소스 등록(연구·용 스킬트리)은 각 매니저의 Construct/Coordinator가
// 이미 수행한다(ConquestResearchCoordinator와 동일한 관용구).
public sealed class ChunkYieldCoordinator : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ChunkYieldMultiplierComposite _yieldComposite;

    private void OnEnable()
    {
        if (_gridMap == null || _yieldComposite == null)
        {
            Debug.LogError(
                "[ChunkYieldCoordinator] 참조 누락 - 연구·용 스킬트리 생산 배율이 전혀 적용되지 않습니다.",
                this);
            return;
        }

        _gridMap.YieldMultiplierQuery = _yieldComposite;
    }

    private void OnDisable()
    {
        if (_gridMap != null && ReferenceEquals(_gridMap.YieldMultiplierQuery, _yieldComposite))
        {
            _gridMap.YieldMultiplierQuery = null;
        }
    }
}
