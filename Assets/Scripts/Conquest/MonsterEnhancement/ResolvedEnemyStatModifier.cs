public readonly struct ResolvedEnemyStatModifier
{
    private const float NEUTRAL_MULTIPLIER = 1f;

    public static ResolvedEnemyStatModifier Neutral =>
        new ResolvedEnemyStatModifier(0f, NEUTRAL_MULTIPLIER);

    public float AdditiveBonus { get; }
    public float Multiplier { get; }

    public ResolvedEnemyStatModifier(float additiveBonus, float multiplier)
    {
        AdditiveBonus = additiveBonus;
        Multiplier = multiplier;
    }

    public float Apply(float baseValue) =>
        (baseValue + AdditiveBonus) * Multiplier;

    public ResolvedEnemyStatModifier Accumulate(EnemyStatModifier modifier) =>
        new ResolvedEnemyStatModifier(
            AdditiveBonus + modifier.AdditiveBonus,
            Multiplier * modifier.Multiplier);
}
