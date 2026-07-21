using UnityEngine;

// 생산시설 1종 정의. Tower/TowerData와 동일한 패턴 - 생산시설은 서브클래스 없이 이 데이터로만 종류가 갈린다.
[CreateAssetMenu(menuName = "TowerAndDragon/Resource Production Data")]
public class ResourceProductionData : ScriptableObject
{
    [Tooltip("이 생산시설을 지으려면 풋프린트 전체 셀이 가져야 하는 자원 플래그.")]
    [SerializeField] private ResourceType _requiredResourceNode;

    [Tooltip("정산 시 실제로 생산되는 자원 종류.")]
    [SerializeField] private ResourceType _producedResourceType;

    [Tooltip("배치 가능한 최대 인구.")]
    [SerializeField] private int _populationCapacity;

    [Tooltip("기본 생산량을 내기 위해 필요한 최소 인구. 배치 인원이 이보다 적으면 생산량 0.")]
    [SerializeField] private int _minimumRequiredPopulation;

    [Tooltip("최소 인구를 충족했을 때의 기본 생산량.")]
    [SerializeField] private int _yieldPerCycle;

    [Tooltip("최소 인구를 초과해 배치한 인원 1명당 추가되는 생산량.")]
    [SerializeField] private int _yieldPerExtraPopulation;

    public ResourceType RequiredResourceNode => _requiredResourceNode;
    public ResourceType ProducedResourceType => _producedResourceType;
    public int PopulationCapacity => _populationCapacity;
    public int MinimumRequiredPopulation => _minimumRequiredPopulation;
    public int YieldPerCycle => _yieldPerCycle;
    public int YieldPerExtraPopulation => _yieldPerExtraPopulation;

    // 최소 인구 미달 시 0, 충족 시 기본 생산량 + (최소 초과 인원 수 * 초과 인원당 생산량).
    public int CalculateYield(int assignedPopulation)
    {
        if (assignedPopulation < _minimumRequiredPopulation)
            return 0;

        int extraPopulation = assignedPopulation - _minimumRequiredPopulation;
        return _yieldPerCycle + extraPopulation * _yieldPerExtraPopulation;
    }
}
