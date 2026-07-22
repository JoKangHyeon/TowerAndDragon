using System.Collections.Generic;

public static class EnemyEnhancementResolver
{
    public static EnemyEnhancementSnapshot Resolve(
        IReadOnlyList<EnemyEnhancementProfileSO> profiles,
        MonsterData targetMonster)
    {
        int spawnCountBonus = 0;

        ResolvedEnemyStatModifier maxHealth =
            ResolvedEnemyStatModifier.Neutral;
        ResolvedEnemyStatModifier shieldAmount =
            ResolvedEnemyStatModifier.Neutral;
        ResolvedEnemyStatModifier attackPower =
            ResolvedEnemyStatModifier.Neutral;
        ResolvedEnemyStatModifier moveSpeed =
            ResolvedEnemyStatModifier.Neutral;
        ResolvedEnemyStatModifier spawnInterval =
            ResolvedEnemyStatModifier.Neutral;

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
                    maxHealth = maxHealth.Accumulate(rule.MaxHealth);
                    shieldAmount = shieldAmount.Accumulate(rule.ShieldAmount);
                    attackPower = attackPower.Accumulate(rule.AttackPower);
                    moveSpeed = moveSpeed.Accumulate(rule.MoveSpeed);
                    spawnInterval = spawnInterval.Accumulate(rule.SpawnInterval);
                }
            }
        }

        return new EnemyEnhancementSnapshot(
            spawnCountBonus,
            maxHealth,
            shieldAmount,
            attackPower,
            moveSpeed,
            spawnInterval);
    }
}
