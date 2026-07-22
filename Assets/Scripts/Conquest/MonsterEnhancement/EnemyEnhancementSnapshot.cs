public readonly struct EnemyEnhancementSnapshot
{
    public static EnemyEnhancementSnapshot Neutral =>
        new EnemyEnhancementSnapshot(
            0,
            ResolvedEnemyStatModifier.Neutral,
            ResolvedEnemyStatModifier.Neutral,
            ResolvedEnemyStatModifier.Neutral,
            ResolvedEnemyStatModifier.Neutral,
            ResolvedEnemyStatModifier.Neutral);

    public int SpawnCountBonus { get; }
    public ResolvedEnemyStatModifier MaxHealth { get; }
    public ResolvedEnemyStatModifier ShieldAmount { get; }
    public ResolvedEnemyStatModifier AttackPower { get; }
    public ResolvedEnemyStatModifier MoveSpeed { get; }
    public ResolvedEnemyStatModifier SpawnInterval { get; }

    public EnemyEnhancementSnapshot(
        int spawnCountBonus,
        ResolvedEnemyStatModifier maxHealth,
        ResolvedEnemyStatModifier shieldAmount,
        ResolvedEnemyStatModifier attackPower,
        ResolvedEnemyStatModifier moveSpeed,
        ResolvedEnemyStatModifier spawnInterval)
    {
        SpawnCountBonus = spawnCountBonus;
        MaxHealth = maxHealth;
        ShieldAmount = shieldAmount;
        AttackPower = attackPower;
        MoveSpeed = moveSpeed;
        SpawnInterval = spawnInterval;
    }
}
