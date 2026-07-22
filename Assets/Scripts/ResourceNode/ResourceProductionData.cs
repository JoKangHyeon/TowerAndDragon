using UnityEngine;

// 생산시설 1종 정의. Tower/TowerData와 동일한 패턴 - 생산시설은 서브클래스 없이 이 데이터로만 종류가 갈린다.
[CreateAssetMenu(menuName = "TowerAndDragon/Resource Production Data")]
public class ResourceProductionData : ScriptableObject
{
    [Tooltip("빌드모드 슬롯에 표시할 이름 로컬라이제이션 키.")]
    [SerializeField] private string _nameLocKey;

    [Tooltip("이 생산시설을 지으려면 풋프린트 전체 셀이 가져야 하는 자원 플래그.")]
    [SerializeField] private ResourceType _requiredResourceNode;

    [Tooltip("정산 시 실제로 생산되는 자원 종류.")]
    [SerializeField] private ResourceType _producedResourceType;

    [Tooltip("배치 가능한 최대 인구.")]
    [SerializeField] private int _populationCapacity;

    public string NameLocKey => _nameLocKey;
    public ResourceType RequiredResourceNode => _requiredResourceNode;
    public ResourceType ProducedResourceType => _producedResourceType;
    public int PopulationCapacity => _populationCapacity;

    // 충원율(배치 인구 / 정원)에 비례해 생산량이 오른다 - 최소 인구 문턱 없음(1명만 있어도 그만큼 생산),
    // 상한은 충원율 자체가 1(정원 100%)을 못 넘는 것으로 자연스럽게 걸린다.
    // footprintYield에는 이미 셀별 자원 배율과 연구 강화가 반영되어 있다 - GridMap.GetFootprintYield 참고.
    public int CalculateYield(int footprintYield, float staffingRatio)
    {
        float ratio = Mathf.Clamp01(staffingRatio);
        return Mathf.RoundToInt(footprintYield * ratio);
    }
}
