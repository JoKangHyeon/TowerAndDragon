using UnityEngine;

public class Tower : Building, IDamageable
{
    public bool IsDead => throw new System.NotImplementedException();
    public void TakeDamage(DamageInfo damage)
    {
        throw new System.NotImplementedException();
    }
}
