using System.Collections.Generic;
using NUnit.Framework;

public class BuildingTerrainExposureTests
{
    private readonly Dictionary<TerrainType, float> _exposure = new();

    [Test]
    public void Accumulate_SingleTerrain_IsFullyExposed()
    {
        var footprint = new List<TerrainType>
        {
            TerrainType.Desert,
            TerrainType.Desert,
            TerrainType.Desert,
            TerrainType.Desert,
        };

        BuildingTerrainExposure.Accumulate(footprint, _exposure);

        Assert.That(_exposure.Count, Is.EqualTo(1));
        Assert.That(_exposure[TerrainType.Desert], Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void Accumulate_SplitAcrossTwoTerrains_SplitsByCellCount()
    {
        var footprint = new List<TerrainType>
        {
            TerrainType.Snow,
            TerrainType.Snow,
            TerrainType.Snow,
            TerrainType.Grass,
        };

        BuildingTerrainExposure.Accumulate(footprint, _exposure);

        Assert.That(_exposure[TerrainType.Snow], Is.EqualTo(0.75f).Within(1e-4f));
        Assert.That(_exposure[TerrainType.Grass], Is.EqualTo(0.25f).Within(1e-4f));
    }

    // 도로/물 셀도 분모에 포함돼야 한다 - 그래야 절반이 도로인 건물이 페널티도 절반만 받는다.
    [Test]
    public void Accumulate_IncludesNonPenaltyTerrainsInDenominator()
    {
        var footprint = new List<TerrainType>
        {
            TerrainType.Rock,
            TerrainType.Road,
        };

        BuildingTerrainExposure.Accumulate(footprint, _exposure);

        Assert.That(_exposure[TerrainType.Rock], Is.EqualTo(0.5f).Within(1e-4f));
        Assert.That(_exposure[TerrainType.Road], Is.EqualTo(0.5f).Within(1e-4f));
    }

    [Test]
    public void Accumulate_RatiosAlwaysSumToOne()
    {
        var footprint = new List<TerrainType>
        {
            TerrainType.Snow,
            TerrainType.Rock,
            TerrainType.Desert,
            TerrainType.Grass,
            TerrainType.Snow,
        };

        BuildingTerrainExposure.Accumulate(footprint, _exposure);

        float total = 0f;
        foreach (KeyValuePair<TerrainType, float> pair in _exposure)
        {
            total += pair.Value;
        }

        Assert.That(total, Is.EqualTo(1f).Within(1e-4f));
    }

    [Test]
    public void Accumulate_EmptyFootprint_ClearsResult()
    {
        _exposure[TerrainType.Desert] = 1f;

        BuildingTerrainExposure.Accumulate(new List<TerrainType>(), _exposure);

        Assert.That(_exposure, Is.Empty);
    }
}
