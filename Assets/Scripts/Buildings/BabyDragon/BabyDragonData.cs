using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "TowerAndDragon/Baby Dragon Data", fileName = "BabyDragonData")]
public class BabyDragonData : TowerData, ITowerAuraDataProvider, IElementalAttackData
{
    [Header("Baby Dragon")]
    [Tooltip("먹이가 되는 슬라임 종류를 결정한다 - DragonSlimeTable 참고.")]
    [SerializeField] private DragonType _dragonType;

    [Tooltip("배치 시 BabyDragonPlacementCoordinator가 SpriteRenderer에 적용한다 - 5속성이 프리팹 1개를 공유하므로 이 필드로 색상을 구분한다.")]
    [SerializeField] private Sprite _sprite;

    [Header("Slime Feeding")]
    [Tooltip("이 속성 새끼용 한 마리가 하루에 먹는 기본 슬라임 양. 수치 미확정 - 기획 확정 후 채울 것.")]
    [SerializeField] [Min(0)] private int _baseFeed;

    [Tooltip("같은 속성 새끼용이 한 마리 늘 때마다 '마리당' 먹이에 추가되는 양. 0이면 같은 속성 마리 수와 무관.")]
    [SerializeField] [Min(0)] private int _additionalFeedPerSameType;

    [Tooltip("속성 무관 전체 새끼용이 한 마리 늘 때마다 '마리당' 먹이에 추가되는 양. 0이면 전체 마리 수와 무관.")]
    [SerializeField] [Min(0)] private int _additionalFeedPerTotal;

    [Header("Egg Growth")]
    [Tooltip("부화까지 필요한 누적 일수(슬라임 소비 없이 day count만으로 증가). 0이면 즉시 부화. 수치 미확정.")]
    [SerializeField] [Min(0)] private int _daysToHatch;

    [Tooltip("알 목록 UI에 표시할 속성별 알 스프라이트. 속성마다 이미지가 달라 색 틴트로 구분하지 않는다.")]
    [SerializeField] private Sprite _eggSprite;

    [Header("Area Buff")]
    [Tooltip("버프가 닿는 반경(월드 좌표). Attack.Range와 무관한 별도 값 - Attack을 아예 안 붙인 버프 전용 개체도 버프 반경을 가질 수 있어야 한다. 0이면 버프 없음.")]
    [SerializeField] [Min(0f)] private float _buffRadius;

    [Tooltip("버프 반경 내 Factory 생산량에 곱해지는 배율. Factory.NEUTRAL_YIELD_MULTIPLIER(1)이면 버프 없음 - 0으로 두면 생산이 전멸하므로 기본값을 유지할 것.")]
    [SerializeField] [Min(0f)] private float _buffYieldMultiplier = Factory.NEUTRAL_YIELD_MULTIPLIER;

    [Tooltip("생산량 버프가 적용될 자원 종류 (복수 선택). None이면 어떤 자원도 버프하지 않는다.")]
    [SerializeField] private ResourceType _buffTargetResources;

    [Tooltip("버프 반경 안에서 지형 건설 제한을 해제할 지형 목록. 비어 있으면 해제 없음 - 얼음=화산 지대.")]
    [SerializeField] private TerrainType[] _constructionUnlockTerrains;

    public IReadOnlyList<TerrainType> ConstructionUnlockTerrains => _constructionUnlockTerrains;

    [Tooltip("버프 반경 안에서 이 지형들의 지역 페널티(생산·공속·유지비)를 전부 무효화한다. 건설 해제(_constructionUnlockTerrains)와는 별개 메커니즘 - 시간=사막.")]
    [SerializeField] private TerrainType[] _penaltyMitigationTerrains;

    public IReadOnlyList<TerrainType> PenaltyMitigationTerrains => _penaltyMitigationTerrains;


    [Header("Tower Aura")]
    [SerializeField] private TowerAuraDataSO _towerAura;

    public DragonType DragonType => _dragonType;
    public Sprite Sprite => _sprite;
    public int BaseFeed => _baseFeed;
    public int AdditionalFeedPerSameType => _additionalFeedPerSameType;
    public int AdditionalFeedPerTotal => _additionalFeedPerTotal;
    public int DaysToHatch => _daysToHatch;
    public Sprite EggSprite => _eggSprite;
    public float BuffRadius => _buffRadius;
    public float BuffYieldMultiplier => _buffYieldMultiplier;
    public ResourceType BuffTargetResources => _buffTargetResources;

    public TowerAuraDataSO TowerAura => _towerAura;

    public bool HasTowerAura =>
        _towerAura != null && _towerAura.HasArea;
}
