using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 건물 배치 세이브 DTO의 정규화·직렬화 계약.
// 여기서 막고 싶은 것은 "세이브 파일이 조용히 엉뚱한 건물을 되살리는" 경우다 -
// 특히 새끼용 인덱스는 두 DTO(Map/Run)에 걸쳐 있어 한쪽만 정리되면 결속이 어긋난다.
public class BuildingPlacementDtoTests
{
    private static BuildingPlacementDto CreatePlacement(string prefabId) => new BuildingPlacementDto
    {
        PrefabId = prefabId,
        Anchor = Vector3IntDto.From(Vector3Int.zero),
        BabyDragonIndex = BuildingPlacementDto.NO_BABY_DRAGON_INDEX,
    };

    [TestCase(-1, 3)]
    [TestCase(0, 0)]
    [TestCase(4, 0)]
    [TestCase(5, 1)]
    [TestCase(-5, 3)]
    public void Normalize_RotationSteps_WrapsIntoRange(int rawSteps, int expectedSteps)
    {
        BuildingPlacementDto placement = CreatePlacement("TP_Arrow");
        placement.RotationSteps = rawSteps;

        placement.Normalize();

        Assert.That(placement.RotationSteps, Is.EqualTo(expectedSteps));
    }

    [Test]
    public void Normalize_NegativeAmounts_ClampToZero()
    {
        BuildingPlacementDto placement = CreatePlacement("TP_Arrow");
        placement.AssignedPopulation = -3;
        placement.ConstructedCycle = -1;

        placement.Normalize();

        Assert.That(placement.AssignedPopulation, Is.EqualTo(0));
        Assert.That(placement.ConstructedCycle, Is.EqualTo(0));
    }

    [Test]
    public void MapNormalize_DropsEntriesThatCannotBeRestored()
    {
        var map = new MapStateDto
        {
            Buildings = new List<BuildingPlacementDto>
            {
                CreatePlacement("TP_Arrow"),
                CreatePlacement(string.Empty),
                CreatePlacement("   "),
                null,
                new BuildingPlacementDto { PrefabId = "TP_Musket", Anchor = null },
            },
        };

        map.Normalize();

        Assert.That(map.Buildings.Count, Is.EqualTo(1));
        Assert.That(map.Buildings[0].PrefabId, Is.EqualTo("TP_Arrow"));
    }

    [Test]
    public void NormalizeBabyDragonReferences_IndexOutOfRange_UnbindsRecord()
    {
        BuildingPlacementDto placement = CreatePlacement("BabyDragonTower");
        placement.BabyDragonIndex = 2;

        var map = new MapStateDto { Buildings = new List<BuildingPlacementDto> { placement } };
        map.Normalize();

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("새끼용 인덱스"));
        map.NormalizeBabyDragonReferences(1);

        Assert.That(placement.BabyDragonIndex, Is.EqualTo(BuildingPlacementDto.NO_BABY_DRAGON_INDEX));
    }

    [Test]
    public void NormalizeBabyDragonReferences_DuplicateIndex_KeepsOnlyTheFirst()
    {
        BuildingPlacementDto first = CreatePlacement("BabyDragonTower");
        first.BabyDragonIndex = 0;

        BuildingPlacementDto second = CreatePlacement("BabyDragonTower");
        second.BabyDragonIndex = 0;

        var map = new MapStateDto { Buildings = new List<BuildingPlacementDto> { first, second } };
        map.Normalize();

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("새끼용 인덱스"));
        map.NormalizeBabyDragonReferences(1);

        Assert.That(first.BabyDragonIndex, Is.EqualTo(0));
        Assert.That(second.BabyDragonIndex, Is.EqualTo(BuildingPlacementDto.NO_BABY_DRAGON_INDEX));
    }

    [Test]
    public void NormalizeBabyDragonReferences_ValidIndex_IsKept()
    {
        BuildingPlacementDto placement = CreatePlacement("BabyDragonTower");
        placement.BabyDragonIndex = 1;

        var map = new MapStateDto { Buildings = new List<BuildingPlacementDto> { placement } };
        map.Normalize();
        map.NormalizeBabyDragonReferences(2);

        Assert.That(placement.BabyDragonIndex, Is.EqualTo(1));
    }

    [Test]
    public void SaveJson_RoundTrip_PreservesEveryField()
    {
        var original = new BuildingPlacementDto
        {
            PrefabId = "Factory_SnowCrystal",
            Anchor = Vector3IntDto.From(new Vector3Int(-4, 7, 0)),
            RotationSteps = 2,
            AssignedPopulation = 5,
            ConstructedCycle = 3,
            BabyDragonIndex = 1,
        };

        Assert.That(SaveJson.TrySerialize(original, out string json, out string serializeError), Is.True, serializeError);
        Assert.That(
            SaveJson.TryDeserialize(json, out BuildingPlacementDto restored, out string deserializeError),
            Is.True,
            deserializeError);

        Assert.That(restored.PrefabId, Is.EqualTo(original.PrefabId));
        Assert.That(restored.Anchor.ToVector3Int(), Is.EqualTo(original.Anchor.ToVector3Int()));
        Assert.That(restored.RotationSteps, Is.EqualTo(original.RotationSteps));
        Assert.That(restored.AssignedPopulation, Is.EqualTo(original.AssignedPopulation));
        Assert.That(restored.ConstructedCycle, Is.EqualTo(original.ConstructedCycle));
        Assert.That(restored.BabyDragonIndex, Is.EqualTo(original.BabyDragonIndex));
    }

    // 건물 배치 도입 이전에 저장된 슬롯은 Buildings 자체가 없다 - 예외 없이 "건물 없음"으로 열려야 한다.
    [Test]
    public void MapNormalize_MissingBuildingsField_BecomesEmptyList()
    {
        Assert.That(
            SaveJson.TryDeserialize("{\"VisibleChunks\":[]}", out MapStateDto map, out string error),
            Is.True,
            error);

        map.Normalize();
        map.NormalizeBabyDragonReferences(0);

        Assert.That(map.Buildings, Is.Not.Null);
        Assert.That(map.Buildings, Is.Empty);
    }
}
