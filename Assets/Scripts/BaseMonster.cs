using UnityEngine;

public class BaseMonster : MonoBehaviour
{
    public enum MonsterType
    {
        Normal,
        NormalAttack,
        Flying,
        FlyingAttack
    }

    private float _monsterHealth;
    private float _monsterSpeed;
    private float _monsterAttack;
    private float _monsterName;
    //private float _shieldAmount; 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
