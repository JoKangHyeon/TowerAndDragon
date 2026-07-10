using UnityEngine;


[CreateAssetMenu(menuName = "TowerAndDragon/TowerData", fileName = "TowerData")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    //[Tooltip("스트링테이블 key. 코드에 직접 이름 문자열을 넣지 않는다.")]
    [SerializeField] private string _nameLocKey;
    [SerializeField] private float _maxHealth;

    //새끼용 이나 연구를 통한 타워 방어막 부여
    [Header("Shield (appliable)")]  

    [SerializeField] private bool _isShieldOn;
    [SerializeField] private float _shieldAmount;


    public string NameLocKey => _nameLocKey;
    public float MaxHealth => _maxHealth;
    public bool IsShieldOn => _isShieldOn;
    public float ShieldAmount => _shieldAmount;
}