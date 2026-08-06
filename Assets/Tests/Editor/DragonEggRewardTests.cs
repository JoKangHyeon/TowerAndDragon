using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DragonEggRewardTests
{
    private const int ATTRIBUTE_COUNT = 5;
    private const int DEFAULT_WEIGHT = 1;

    private DragonEggRewardPoolSO _pool;

    [SetUp]
    public void SetUp()
    {
        _pool = ScriptableObject.CreateInstance<DragonEggRewardPoolSO>();
        SerializedObject serializedPool = new SerializedObject(_pool);
        SerializedProperty entries = serializedPool.FindProperty("_entries");
        entries.arraySize = ATTRIBUTE_COUNT;

        for (int index = 0; index < ATTRIBUTE_COUNT; index++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("DragonType").enumValueIndex = index;
            entry.FindPropertyRelative("Weight").intValue = DEFAULT_WEIGHT;
        }

        serializedPool.ApplyModifiedPropertiesWithoutUndo();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_pool);
    }

    [TestCase(0, DragonType.Ice)]
    [TestCase(1, DragonType.Fire)]
    [TestCase(2, DragonType.Time)]
    [TestCase(3, DragonType.Stone)]
    [TestCase(4, DragonType.Life)]
    public void TryResolveRoll_EqualWeights_MapsEveryAttribute(
        int roll,
        DragonType expectedType)
    {
        bool resolved = _pool.TryResolveRoll(roll, out DragonType dragonType);

        Assert.That(resolved, Is.True);
        Assert.That(dragonType, Is.EqualTo(expectedType));
    }

    [Test]
    public void TryRecordBossDragonEggReward_DifferentCycles_AllowsDuplicateAttribute()
    {
        RunData run = new RunData();

        bool firstRecorded = run.TryRecordBossDragonEggReward(
            WaveCycleRules.FIRST_CYCLE_NUMBER,
            DragonType.Fire);
        bool secondRecorded = run.TryRecordBossDragonEggReward(
            WaveCycleRules.FIRST_CYCLE_NUMBER + 1,
            DragonType.Fire);

        Assert.That(firstRecorded, Is.True);
        Assert.That(secondRecorded, Is.True);
        Assert.That(run.BossDragonEggRewards, Has.Count.EqualTo(2));
    }

    [Test]
    public void TryRecordBossDragonEggReward_SameCycle_RejectsSecondReward()
    {
        RunData run = new RunData();
        int cycleNumber = WaveCycleRules.FIRST_CYCLE_NUMBER;

        bool firstRecorded = run.TryRecordBossDragonEggReward(cycleNumber, DragonType.Ice);
        bool secondRecorded = run.TryRecordBossDragonEggReward(cycleNumber, DragonType.Life);

        Assert.That(firstRecorded, Is.True);
        Assert.That(secondRecorded, Is.False);
        Assert.That(run.BossDragonEggRewards, Has.Count.EqualTo(1));
        Assert.That(run.BossDragonEggRewards[0].DragonType, Is.EqualTo(DragonType.Ice));
    }

    [Test]
    public void Normalize_LegacySaveWithoutRewardList_ProducesEmptyList()
    {
        RunStateDto dto = new RunStateDto();

        dto.Normalize();

        Assert.That(dto.BossDragonEggRewards, Is.Not.Null);
        Assert.That(dto.BossDragonEggRewards, Is.Empty);
    }

    [Test]
    public void Normalize_UndefinedDragonType_DropsRewardEntry()
    {
        const int UNDEFINED_DRAGON_TYPE = 99;

        RunStateDto dto = new RunStateDto
        {
            BossDragonEggRewards = new List<BossDragonEggRewardDto>
            {
                new BossDragonEggRewardDto
                {
                    CycleNumber = WaveCycleRules.FIRST_CYCLE_NUMBER,
                    DragonType = UNDEFINED_DRAGON_TYPE,
                },
            },
        };

        LogAssert.Expect(LogType.Warning, new Regex(UNDEFINED_DRAGON_TYPE.ToString()));

        dto.Normalize();

        Assert.That(dto.BossDragonEggRewards, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(WaveCycleRules.MAX_CYCLE_COUNT + 1)]
    public void Normalize_OutOfRangeCycleNumber_DropsRewardEntry(int cycleNumber)
    {
        RunStateDto dto = new RunStateDto
        {
            BossDragonEggRewards = new List<BossDragonEggRewardDto>
            {
                new BossDragonEggRewardDto
                {
                    CycleNumber = cycleNumber,
                    DragonType = (int)DragonType.Fire,
                },
            },
        };

        LogAssert.Expect(LogType.Warning, new Regex(cycleNumber.ToString()));

        dto.Normalize();

        Assert.That(dto.BossDragonEggRewards, Is.Empty);
    }

    [Test]
    public void Normalize_ValidReward_IsKept()
    {
        RunStateDto dto = new RunStateDto
        {
            BossDragonEggRewards = new List<BossDragonEggRewardDto>
            {
                new BossDragonEggRewardDto
                {
                    CycleNumber = WaveCycleRules.MAX_CYCLE_COUNT,
                    DragonType = (int)DragonType.Life,
                },
            },
        };

        dto.Normalize();

        Assert.That(dto.BossDragonEggRewards, Has.Count.EqualTo(1));
        Assert.That(
            dto.BossDragonEggRewards[0].CycleNumber,
            Is.EqualTo(WaveCycleRules.MAX_CYCLE_COUNT));
        Assert.That(
            dto.BossDragonEggRewards[0].DragonType,
            Is.EqualTo((int)DragonType.Life));
    }
}
