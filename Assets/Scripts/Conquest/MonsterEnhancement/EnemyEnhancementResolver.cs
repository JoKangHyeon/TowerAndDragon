using System.Collections.Generic;

public static class EnemyEnhancementResolver
{
    private const float NEUTRAL_MULTIPLIER = 1f;

    public static EnemyEnhancementSnapshot Resolve(
        IReadOnlyList<EnemyEnhancementProfileSO> profiles,
        MonsterData targetMonster)
    {
        int spawnCountBonus = 0;

        float maxHealthAdditive = 0f;
        float maxHealthMultiplier = NEUTRAL_MULTIPLIER;
        float shieldAdditive = 0f;
        float shieldMultiplier = NEUTRAL_MULTIPLIER;
        float attackAdditive = 0f;
        float attackMultiplier = NEUTRAL_MULTIPLIER;
        float moveSpeedAdditive = 0f;
        float moveSpeedMultiplier = NEUTRAL_MULTIPLIER;
        float spawnIntervalAdditive = 0f;
        float spawnIntervalMultiplier = NEUTRAL_MULTIPLIER;

        if (profiles != null && targetMonster != null)
        {
            foreach (EnemyEnhancementProfileSO profile in profiles)
            {
                if (profile == null)
                {
                    continue;
                }

                foreach (EnemyEnhancementRule rule in profile.Rules)
                {
                    if (rule == null || rule.TargetMonster != targetMonster)
                    {
                        continue;
                    }

                    spawnCountBonus += rule.SpawnCountBonus;
                    Accumulate(
                        ref maxHealthAdditive,
                        ref maxHealthMultiplier,
                        rule.MaxHealth);
                    Accumulate(
                        ref shieldAdditive,
                        ref shieldMultiplier,
                        rule.ShieldAmount);
                    Accumulate(
                        ref attackAdditive,
                        ref attackMultiplier,
                        rule.AttackPower);
                    Accumulate(
                        ref moveSpeedAdditive,
                        ref moveSpeedMultiplier,
                        rule.MoveSpeed);
                    Accumulate(
                        ref spawnIntervalAdditive,
                        ref spawnIntervalMultiplier,
                        rule.SpawnInterval);
                }
            }
        }

        return new EnemyEnhancementSnapshot(
            spawnCountBonus,
            new ResolvedEnemyStatModifier(maxHealthAdditive, maxHealthMultiplier),
            new ResolvedEnemyStatModifier(shieldAdditive, shieldMultiplier),
            new ResolvedEnemyStatModifier(attackAdditive, attackMultiplier),
            new ResolvedEnemyStatModifier(moveSpeedAdditive, moveSpeedMultiplier),
            new ResolvedEnemyStatModifier(spawnIntervalAdditive, spawnIntervalMultiplier));
    }

    private static void Accumulate(
        ref float additive,
        ref float multiplier,
        EnemyStatModifier modifier)
    {
        additive += modifier.AdditiveBonus;
        multiplier *= modifier.Multiplier;
    }
}
