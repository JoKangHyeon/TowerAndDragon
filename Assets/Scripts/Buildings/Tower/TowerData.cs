using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "TowerAndDragon/Tower Data", fileName = "TowerData")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    //[Tooltip("스트링테이블 key. 코드에 직접 이름 문자열을 넣지 않는다.")]
    [SerializeField] private string _nameLocKey;

    [Tooltip("켜면 연구로 해금하기 전까지 건설 메뉴에 나오지 않는다. 기본 타워는 꺼 둔다.")]
    [SerializeField] private bool _requiresResearchUnlock;

    // 건설 메뉴 슬롯은 프리팹의 SpriteRenderer에서 아이콘을 직접 꺼내 쓰므로(UI_BuildingSlot.ResolveIcon)
    // 이 필드가 없어도 동작한다. 알림 카드처럼 프리팹이 아니라 TowerData만 들고 있는 쪽을 위한 값이다.
    [Tooltip("알림 카드 등 프리팹 없이 이 타워를 가리키는 UI에 띄울 이미지. 비워두면 이미지 없이 표시된다.")]
    [SerializeField] private Sprite _icon;

    [Header("Population")]
    [SerializeField]
    [Min(1)]
    private int _populationCapacity = 1;

    [Tooltip("건설 비용. 자원 종류 + 수량 조합을 자유롭게 지정한다. (수치 미확정 - 기획 확정 후 채울 것)")]
    [SerializeField] private ResourceAmount[] _buildCost;

    [Header("Durability")]
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _reviveDelay;

    [Header("Attack")]
    [SerializeField] private AttackSO _attack;

    // AttackSO가 아니라 여기에 두는 이유: 공격 애셋은 타워끼리 공유된다
    // (예: TA_Arrow를 궁수 타워와 시간 새끼용이 함께 쓴다). 공격 쪽에 두면 한쪽을 대공 전용으로
    // 바꿀 때 다른 쪽까지 조용히 따라 바뀐다.
    [Tooltip("이 타워가 노릴 수 있는 적의 이동 방식. All이면 지상·공중을 모두 공격한다.")]
    [SerializeField] private TargetMovementFilter _targetMovementFilter;

    // 원거리 타워용 투사체. 비워두면 즉시 적용 공격으로 동작한다.
    // 피해는 투사체 명중 시점에 적용된다. (MonsterData와 동일한 패턴)
    [Header("Projectile")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _projectileSpeed;

    // 오라 타워는 TowerAuraDataSO가 자기 범위 마커를 들고 있다. 오라가 없는 타워도 클릭했을 때
    // 같은 언어로 범위를 보여 주고 싶을 때가 있어(생명 타워) 이쪽에 자리를 하나 둔다.
    // 반경은 이 데이터가 아니라 실제 유효 사거리(TowerAttack.EffectiveRange)를 쓴다 -
    // 선(RangeIndicator)과 파티클이 어긋나면 플레이어가 버그로 읽는다.
    // 비워 두는 것이 기본이다 - 안 꽂으면 아무것도 뜨지 않는다.
    [Header("VFX")]
    [WiringOptional]
    [Tooltip("이 타워를 클릭했을 때 발밑에 깔 사거리 마커 프리팹. 비우면 표시하지 않는다.")]
    [SerializeField] private GameObject _attackRangeMarkerPrefab;

    // 오라 타워의 수혜자 구체(TowerAuraDataSO.RecipientOrbPrefab)와 같은 자리다. 아군을 회복하는
    // 타워는 오라가 아니라 TowerAllyHealer로 대상을 잡으므로 오라 데이터에 실을 곳이 없다.
    [WiringOptional]
    [Tooltip("아군을 회복하는 타워를 클릭했을 때, 사거리 안 타워 머리 위에 띄울 구체 프리팹. " +
             "비우면 표시하지 않는다.")]
    [SerializeField] private GameObject _allyHealOrbPrefab;

    [Header("Sound")]
    [Tooltip("공격 또는 회복 투사체를 발동하는 순간 재생할 효과음.")]
    [SerializeField] private OptionalSoundId _launchSound;

    [Tooltip("투사체가 목적지에 도착해 공격 또는 회복 효과를 적용할 때 재생할 효과음.")]
    [SerializeField] private OptionalSoundId _resolveSound;

    [Tooltip("오라가 비활성에서 활성으로 바뀔 때 한 번 재생할 효과음.")]
    [SerializeField] private OptionalSoundId _auraOnSound;

    public SoundId? LaunchSound => _launchSound.Value;
    public SoundId? ResolveSound => _resolveSound.Value;
    public SoundId? AuraOnSound => _auraOnSound.Value;


    public string NameLocKey => _nameLocKey;
    public virtual TowerCategory Category => TowerCategory.Basic;
    public bool RequiresResearchUnlock => _requiresResearchUnlock;
    public Sprite Icon => _icon;
    public virtual int PopulationCapacity => _populationCapacity;
    public IReadOnlyList<ResourceAmount> BuildCost => _buildCost ?? System.Array.Empty<ResourceAmount>();
    public float MaxHealth => _maxHealth;
    public float ReviveDelay => _reviveDelay;
    public AttackSO Attack => _attack;
    public bool CanAttack => _attack != null;
    public TargetMovementFilter TargetMovementFilter => _targetMovementFilter;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public float ProjectileSpeed => _projectileSpeed;
    public bool HasProjectile => _projectilePrefab != null && _projectileSpeed > 0;

    public GameObject AttackRangeMarkerPrefab => _attackRangeMarkerPrefab;
    public bool HasAttackRangeMarker => _attackRangeMarkerPrefab != null;

    public GameObject AllyHealOrbPrefab => _allyHealOrbPrefab;
    public bool HasAllyHealOrb => _allyHealOrbPrefab != null;
}
