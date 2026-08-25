using UnityEngine;


[RequireComponent(typeof(Health))]
public sealed class ThornTower : Tower, ITowerStaffing
{
    public override bool RequiresPopulation => false;
    public override int PopulationCapacity => 0;
    public bool CanOperate => true;
    public float StaffingRatio => 1f;

    public override void TakeDamage(DamageInfo damage)
    {
        if (IsDead)
        {
            return;
        }

        base.TakeDamage(damage);

        if (damage.Source == null || Data is not ThornTowerData thornData)
        {
            return;            
        }

        BaseMonster attacker = damage.Source.GetComponentInParent<BaseMonster>();

        if (attacker == null || attacker.IsDead || thornData.ThornDamage <= 0f)
        {
            return;
        }

        attacker.TakeDamage(new DamageInfo(thornData.ThornDamage, null, gameObject));
    }
}
