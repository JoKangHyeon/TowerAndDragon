using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 한 종류의 스탯/구성 데이터. 코드 수정 없이 에셋만 복제해
/// "공중 원거리 쉴드" 같은 변형을 기획자가 직접 만들 수 있도록 SO로 둔다.
/// (속성 시스템은 현재 미도입 — 추후 방어막 쪽에 추가 예정)
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Monster Data", fileName = "MonsterData")]
public class MonsterData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("스트링테이블 key. 코드에 직접 이름 문자열을 넣지 않는다.")]
    [SerializeField] private string _nameLocKey;

    [Tooltip("출현 예고 툴팁에 띄울 설명문의 스트링테이블 key. 비워두면 스탯 줄만 표시한다.")]
    [SerializeField] private string _descriptionLocKey;

    [Header("Monster Type")]
    [Tooltip("몬스터 종류")]
    [SerializeField] private MonsterType _monsterType;

    [Header("Stats")]
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private MonsterMovementType _movementType;

    [Header("Shield (optional)")]
    [SerializeField] private bool _hasShield;
    [SerializeField] private float _shieldAmount;

    [Header("Crowd Control Defense")]
    [Tooltip("둔화·빙결·경직 등 모든 군중제어와 이동속도 변화 상태에 면역. 보스 몬스터가 켠다.")]
    [SerializeField] private bool _isCrowdControlImmune;

    [Header("Elemental Defense")]
    [SerializeField] private MonsterElementRule _elementRule;
    [SerializeField] private DragonType _element;

    [Header("Attack")]
    [SerializeField] private AttackSO _attack;
    [SerializeField] private MonsterTargetType _enRouteTargetTypes;

    [Header("Special Behaviours")]
    [SerializeField] private SpecialBehaviorSO[] _specialBehaviors;

    // 원거리 몬스터용 투사체. 비워두면 즉시 적용(근접) 공격으로 동작한다.
    // 타워와 달리 피해는 투사체 명중 시점에 적용된다.
    [Header("Projectile (optional)")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _projectileSpeed;

    public string NameLocKey => _nameLocKey;
    public string DescriptionLocKey => _descriptionLocKey;
    public MonsterType MonsterType => _monsterType;
    public float MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public MonsterMovementType MovementType => _movementType;

    public bool HasShield => _hasShield;
    public float ShieldAmount => _shieldAmount;

    public bool IsCrowdControlImmune => _isCrowdControlImmune;

    public MonsterElementRule ElementRule => _elementRule;
    public DragonType Element => _element;

    public AttackSO Attack => _attack;
    public MonsterTargetType EnRouteTargetTypes => _enRouteTargetTypes;

    public IReadOnlyList<SpecialBehaviorSO> SpecialBehaviors => _specialBehaviors;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public float ProjectileSpeed => _projectileSpeed;
    public bool HasProjectile => _projectilePrefab != null && _projectileSpeed > 0;

    public bool AcceptsElement(DragonType? attackElement)
    {
        return _elementRule switch
        {
            MonsterElementRule.Normal => true,

            MonsterElementRule.OnlyMatchingElement =>
                attackElement.HasValue &&
                attackElement.Value == _element,

            MonsterElementRule.ImmuneToMatchingElement =>
                !attackElement.HasValue ||
                attackElement.Value != _element,

            MonsterElementRule.AllImmune =>
                !attackElement.HasValue,

            _ => true,
        };
    }
}
