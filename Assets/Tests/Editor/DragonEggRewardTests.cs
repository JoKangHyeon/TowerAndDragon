using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
}
