using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "TowerAndDragon/Tower Data", fileName = "TowerData")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    //[Tooltip("스트링테이블 key. 코드에 직접 이름 문자열을 넣지 않는다.")]
    [SerializeField] private string _nameLocKey;

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


    public string NameLocKey => _nameLocKey;
    public int PopulationCapacity => _populationCapacity;
    public IReadOnlyList<ResourceAmount> BuildCost => _buildCost ?? System.Array.Empty<ResourceAmount>();
    public float MaxHealth => _maxHealth;
    public float ReviveDelay => _reviveDelay;
    public AttackSO Attack => _attack;
    public bool CanAttack => _attack != null;
    public TargetMovementFilter TargetMovementFilter => _targetMovementFilter;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public float ProjectileSpeed => _projectileSpeed;
    public bool HasProjectile => _projectilePrefab != null && _projectileSpeed > 0;
}
