using System.Collections.Generic;
using UnityEngine;

// 랜드마크 원형 1종의 정의(에셋 1개 = 종류 1개). 어느 청크에 놓이는지는 여기가 아니라
// ChunkLandmarkTable이 정한다 - 같은 원형을 여러 청크에 배치할 수 있어야 하기 때문이다.
//
// 보상(_conquestRewards)과 가동 효과(_operationEffects)를 분리한 이유:
// 보상은 점령 완료 시 딱 한 번 지급되고, 가동 효과는 인구가 들어가 있는 동안만 유지된다.
// 한 배열로 합치면 "1회성인가 지속인가"를 효과 쪽에서 다시 판별해야 한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Landmark/Landmark Data",
    fileName = "LandmarkData")]
public sealed class LandmarkDataSO : ScriptableObject
{
    [Tooltip("세이브·연구 조건이 참조하는 안정 ID. 에셋 이름을 바꿔도 이 값은 유지해야 한다.")]
    [SerializeField] private string _landmarkId;

    [SerializeField] private string _nameLocKey;
    [SerializeField] private string _descriptionLocKey;

    [Tooltip("점령 패널·청크 카드·툴팁에 공통으로 쓰는 아이콘.")]
    [SerializeField] private Sprite _icon;

    [Tooltip("청크 중심에 세울 시각물. 비워두면 마커만 표시된다.")]
    [SerializeField] private GameObject _worldPrefab;

    [Tooltip("투입 가능한 인구 정원. 0이면 가동할 수 없는(보상 전용) 랜드마크다.")]
    [Min(0)]
    [SerializeField] private int _populationCapacity;

    [Tooltip("점령 완료 시 한 번만 지급되는 보상.")]
    [SerializeField] private LandmarkRewardSO[] _conquestRewards;

    [Tooltip("인구가 배치되어 있는 동안에만 적용되는 지속 효과.")]
    [SerializeField] private LandmarkEffectSO[] _operationEffects;

    public string LandmarkId => _landmarkId;
    public string NameLocKey => _nameLocKey;
    public string DescriptionLocKey => _descriptionLocKey;
    public Sprite Icon => _icon;
    public GameObject WorldPrefab => _worldPrefab;
    public int PopulationCapacity => _populationCapacity;

    // ResearchNodeData와 동일한 null-가드 게터 관례.
    public IReadOnlyList<LandmarkRewardSO> ConquestRewards =>
        _conquestRewards ?? System.Array.Empty<LandmarkRewardSO>();

    public IReadOnlyList<LandmarkEffectSO> OperationEffects =>
        _operationEffects ?? System.Array.Empty<LandmarkEffectSO>();

    // 정원이 0인 랜드마크에는 인구 할당 자체를 만들지 않는다.
    public bool IsOperable => _populationCapacity > 0;
}
