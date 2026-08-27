using System.Collections.Generic;

public static class EnemyEnhancementResolver
{
    public static EnemyEnhancementSnapshot Resolve(
        IReadOnlyList<EnemyEnhancementProfileSO> profiles,
        MonsterData targetMonster)
    {
        // 속성 전용형은 특정 속성의 공격을 강제해 초반부터 대응 선택지를 잠근다.
        // 웨이브에서 제외하는 정책과 함께, 기존/추가 점령 프로필이 실수로 이 형을
        // 강화하지 않도록 방어한다.
        if (targetMonster != null &&
            targetMonster.ElementRule == MonsterElementRule.OnlyMatchingElement)
        {
            return EnemyEnhancementSnapshot.Neutral;
        }

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
