using UnityEngine;

// 생산시설 1종 정의. Tower/TowerData와 동일한 패턴 - 생산시설은 서브클래스 없이 이 데이터로만 종류가 갈린다.
[CreateAssetMenu(menuName = "TowerAndDragon/Resource Production Data")]
public class ResourceProductionData : ScriptableObject
{
    [Tooltip("이 생산시설을 지으려면 풋프린트 전체 셀이 가져야 하는 자원 플래그.")]
    [SerializeField] private ResourceType _requiredResourceNode;

    [Tooltip("정산 시 실제로 생산되는 자원 종류.")]
    [SerializeField] private ResourceType _producedResourceType;

    [SerializeField] private int _yieldPerCycle;

    public ResourceType RequiredResourceNode => _requiredResourceNode;
    public ResourceType ProducedResourceType => _producedResourceType;
    public int YieldPerCycle => _yieldPerCycle;
}
