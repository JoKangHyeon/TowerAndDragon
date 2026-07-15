using UnityEngine;


[CreateAssetMenu(menuName = "TowerAndDragon/Tower Data", fileName = "TowerData")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    //[Tooltip("스트링테이블 key. 코드에 직접 이름 문자열을 넣지 않는다.")]
    [SerializeField] private string _nameLocKey;

    [Header("Durability")]
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _reviveDelay;

    [Header("Attack")]
    [SerializeField] private AttackSO _attack;

    // 원거리 타워용 투사체. 비워두면 즉시 적용 공격으로 동작한다.
    // 피해는 투사체 명중 시점에 적용된다. (MonsterData와 동일한 패턴)
    [Header("Projectile")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _projectileSpeed;


    public string NameLocKey => _nameLocKey;
    public float MaxHealth => _maxHealth;
    public float ReviveDelay => _reviveDelay;
    public AttackSO Attack => _attack;
    public bool CanAttack => _attack != null;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public float ProjectileSpeed => _projectileSpeed;
    public bool HasProjectile => _projectilePrefab != null && _projectileSpeed > 0;
}
