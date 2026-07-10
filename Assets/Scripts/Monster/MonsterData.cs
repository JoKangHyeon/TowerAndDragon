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

    [Header("Stats")]
    [SerializeField] private float _maxHealth;
    [SerializeField] private float _moveSpeed;
    [SerializeField] private MonsterMovementType _movementType;

    [Header("Shield (optional)")]
    [SerializeField] private bool _hasShield;
    [SerializeField] private float _shieldAmount;

    [Header("Attack (optional)")]
    [Tooltip("공격형이면 AttackSO를 지정한다. 비워 두면 비공격형으로 간주한다.")]
    [SerializeField] private AttackSO _attack;

    public string NameLocKey => _nameLocKey;
    public float MaxHealth => _maxHealth;
    public float MoveSpeed => _moveSpeed;
    public MonsterMovementType MovementType => _movementType;

    public bool HasShield => _hasShield;
    public float ShieldAmount => _shieldAmount;

    public AttackSO Attack => _attack;
    public bool IsAttacker => _attack != null;
}
