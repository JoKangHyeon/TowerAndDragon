using System.Collections.Generic;
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

    [Tooltip("건설 비용. 자원 종류(특화 자원 포함) + 수량 조합을 자유롭게 지정한다.")]
    [SerializeField] private ResourceAmount[] _buildCost;

    public string NameLocKey => _nameLocKey;
    public ResourceType RequiredResourceNode => _requiredResourceNode;
    public ResourceType ProducedResourceType => _producedResourceType;
    public int PopulationCapacity => _populationCapacity;
    public IReadOnlyList<ResourceAmount> BuildCost => _buildCost ?? System.Array.Empty<ResourceAmount>();

    // 산출 계산식은 여기 없다 - FactoryYieldRules에 모아 두었다(정산·상한 표시·배치 미리보기가 공유).
}
