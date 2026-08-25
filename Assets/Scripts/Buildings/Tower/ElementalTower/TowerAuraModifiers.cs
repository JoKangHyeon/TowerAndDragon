using UnityEngine;


// 여러 오라가 겹쳤을때 가장 강한 값만 적용하기 위한 중복 방지 시스템
public readonly struct TowerAuraModifiers
{
    public float DamageMultiplier { get; }
    public float AttackSpeedMultiplier { get; }
    public float ReviveSpeedMultiplier { get; }
    public float MaxHealthMultiplier { get; }
    public float ShieldAmount { get; }
    public bool IsStealth { get; }
    public int SoulPopulation { get; }

    public static TowerAuraModifiers Neutral =>
        new TowerAuraModifiers (1f,1f,1f,1f,0f, false, 0);
    
    public TowerAuraModifiers(
        float damageMultiplier,
        float attackSpeedMultiplier,
        float reviveSpeedMultiplier,
        float maxHealthMultiplier,
        float shieldAmount,
        bool isStealth,
        int soulPopulation
    )
    {
        DamageMultiplier = damageMultiplier;
        AttackSpeedMultiplier = attackSpeedMultiplier;
        ReviveSpeedMultiplier = reviveSpeedMultiplier;
        MaxHealthMultiplier = maxHealthMultiplier;
        ShieldAmount = shieldAmount;
        IsStealth = isStealth;
        SoulPopulation = soulPopulation;
    }

    public TowerAuraModifiers CombineStrongest(
        TowerAuraDataSO aura
    )
    {
        if (aura == null)
        {
            return this;
        }

        return new TowerAuraModifiers(
            Mathf.Max(DamageMultiplier, aura.DamageMultiplier),
            Mathf.Max(
                AttackSpeedMultiplier,
                aura.AttackSpeedMultiplier),

            Mathf.Max(
                ReviveSpeedMultiplier,
                aura.ReviveSpeedMultiplier),
            
            Mathf.Max(
                MaxHealthMultiplier,
                aura.MaxHealthMultiplier),

            Mathf.Max (ShieldAmount, aura.ShieldAmount),
            IsStealth || aura.IsStealth,
            Mathf.Max(SoulPopulation, aura.SoulPopulation));
    }
}