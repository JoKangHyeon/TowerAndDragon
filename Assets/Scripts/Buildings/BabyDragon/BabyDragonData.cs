using UnityEngine;

[CreateAssetMenu(menuName = "TowerAndDragon/Baby Dragon Data", fileName = "BabyDragonData")]
public class BabyDragonData : TowerData
{
    [Header("Baby Dragon")]
    [Tooltip("먹이가 되는 슬라임 종류를 결정한다 - BabyDragonSlimeTable 참고.")]
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
    [Tooltip("알 상태에서 하루에 먹는 슬라임 양. 0이면 즉시 부화 취급(성공 처리). 수치 미확정.")]
    [SerializeField] [Min(0)] private int _eggDailyFeed;

    [Tooltip("부화까지 필요한 누적 '성공적으로 먹은 날' 수. 0이면 즉시 부화. 수치 미확정.")]
    [SerializeField] [Min(0)] private int _daysToHatch;

    [Header("Area Buff")]
    [Tooltip("버프가 닿는 반경(월드 좌표). Attack.Range와 무관한 별도 값 - Attack을 아예 안 붙인 버프 전용 개체도 버프 반경을 가질 수 있어야 한다. 0이면 버프 없음.")]
    [SerializeField] [Min(0f)] private float _buffRadius;

    [Tooltip("버프 반경 내 Factory 생산량에 곱해지는 배율. Factory.NEUTRAL_YIELD_MULTIPLIER(1)이면 버프 없음 - 0으로 두면 생산이 전멸하므로 기본값을 유지할 것.")]
    [SerializeField] [Min(0f)] private float _buffYieldMultiplier = Factory.NEUTRAL_YIELD_MULTIPLIER;

    public DragonType DragonType => _dragonType;
    public Sprite Sprite => _sprite;
    public int BaseFeed => _baseFeed;
    public int AdditionalFeedPerSameType => _additionalFeedPerSameType;
    public int AdditionalFeedPerTotal => _additionalFeedPerTotal;
    public int EggDailyFeed => _eggDailyFeed;
    public int DaysToHatch => _daysToHatch;
    public float BuffRadius => _buffRadius;
    public float BuffYieldMultiplier => _buffYieldMultiplier;
}
