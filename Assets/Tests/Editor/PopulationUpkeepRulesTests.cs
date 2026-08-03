using NUnit.Framework;

public class PopulationUpkeepRulesTests
{
    [TestCase(30, 40, 0, 30, 40, 30, 0)]
    [TestCase(30, 20, 5, 30, 25, 25, 5)]
    [TestCase(30, 20, 20, 30, 40, 30, 0)]
    [TestCase(30, 0, 0, 30, 0, 0, 30)]
    [TestCase(0, 10, 5, 0, 15, 0, 0)]
    [TestCase(-1, 10, 5, 0, 15, 0, 0)]
    [TestCase(10, -5, -2, 10, 0, 0, 10)]
    public void Calculate_ResolvesSettlementPreview(
        int population,
        int currentFood,
        int projectedFoodProduction,
        int expectedRequiredFood,
        int expectedFoodAtSettlement,
        int expectedConsumedFood,
        int expectedPopulationLost
    )
    {
        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            population,
            currentFood,
            projectedFoodProduction
        );

        Assert.That(preview.RequiredFood, Is.EqualTo(expectedRequiredFood));
        Assert.That(preview.FoodAtSettlement, Is.EqualTo(expectedFoodAtSettlement));
        Assert.That(preview.ConsumedFood, Is.EqualTo(expectedConsumedFood));
        Assert.That(preview.PopulationLost, Is.EqualTo(expectedPopulationLost));
    }

    [Test]
    public void Calculate_FoodProjectionOverflows_ClampsToIntMaxValue()
    {
        PopulationUpkeepPreview preview = PopulationUpkeepRules.Calculate(
            int.MaxValue,
            int.MaxValue,
            int.MaxValue
        );

        Assert.That(preview.FoodAtSettlement, Is.EqualTo(int.MaxValue));
        Assert.That(preview.ConsumedFood, Is.EqualTo(int.MaxValue));
        Assert.That(preview.PopulationLost, Is.Zero);
    }
}
