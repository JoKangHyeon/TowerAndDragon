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

    public string NameLocKey => _nameLocKey;
    public float MaxHealth => _maxHealth;
    public float ReviveDelay => _reviveDelay;
    public AttackSO Attack => _attack;
    public bool CanAttack => _attack != null;

}
