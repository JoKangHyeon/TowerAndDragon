using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// 표시(남은 일수)와 판정(부화 여부)이 같은 유효 일수를 보는지가 이 테스트의 요지다.
// 갈라지면 "1일 남음"이라 적힌 알이 부화하지 않는다.
public class DragonEggHatchRulesTests
{
    private const int BASE_DAYS_TO_HATCH = 3;

    private readonly List<ScriptableObject> _created = new();

    [TearDown]
    public void TearDown()
    {
        foreach (ScriptableObject asset in _created)
        {
            if (asset != null)
            {
                Object.DestroyImmediate(asset);
            }
        }

        _created.Clear();
    }

    [Test]
    public void ResolveDaysToHatch_NeutralSnapshot_MatchesAssetValue()
    {
        BabyDragonData data = MakeData(BASE_DAYS_TO_HATCH);

        Assert.That(
            DragonEggHatchRules.ResolveDaysToHatch(data, RunModifierSnapshot.Neutral),
            Is.EqualTo(BASE_DAYS_TO_HATCH));
    }

    [Test]
    public void ResolveDaysToHatch_ExtraHatchDays_AddsToAssetValue()
    {
        BabyDragonData data = MakeData(BASE_DAYS_TO_HATCH);

        Assert.That(
            DragonEggHatchRules.ResolveDaysToHatch(data, SnapshotWithExtraHatchDays(1)),
            Is.EqualTo(BASE_DAYS_TO_HATCH + 1));
    }

    [Test]
    public void ResolveDaysToHatch_NegativeCounter_StopsAtZero()
    {
        BabyDragonData data = MakeData(1);

        Assert.That(
            DragonEggHatchRules.ResolveDaysToHatch(data, SnapshotWithExtraHatchDays(-5)),
            Is.EqualTo(0));
    }

    [Test]
    public void IsReadyToHatch_ExtraHatchDays_DelaysHatchByExactlyOneDay()
    {
        BabyDragonData data = MakeData(BASE_DAYS_TO_HATCH);
        DragonEgg egg = new DragonEgg { FedDayCount = BASE_DAYS_TO_HATCH };
        RunModifierSnapshot heavyGravity = SnapshotWithExtraHatchDays(1);

        // 기본 규칙으로는 오늘 부화할 알이 중력 적응 아래에서는 아직 아니다.
        Assert.That(DragonEggHatchRules.IsReadyToHatch(egg, data, RunModifierSnapshot.Neutral), Is.True);
        Assert.That(DragonEggHatchRules.IsReadyToHatch(egg, data, heavyGravity), Is.False);

        egg.FedDayCount += 1;

        Assert.That(DragonEggHatchRules.IsReadyToHatch(egg, data, heavyGravity), Is.True);
    }

    // 이 테스트가 T5의 실제 요구사항이다 - 표시가 0이 되는 날이 곧 부화하는 날이어야 한다.
    [Test]
    public void ResolveRemainingDays_ReachesZeroOnTheSameDayAsHatch()
    {
        BabyDragonData data = MakeData(BASE_DAYS_TO_HATCH);
        RunModifierSnapshot heavyGravity = SnapshotWithExtraHatchDays(1);
        DragonEgg egg = new DragonEgg { FedDayCount = 0 };

        for (int fed = 0; fed <= BASE_DAYS_TO_HATCH + 1; fed++)
        {
            egg.FedDayCount = fed;

            bool showsZeroDaysLeft =
                DragonEggHatchRules.ResolveRemainingDays(egg, data, heavyGravity) == 0;
            bool hatchesNow = DragonEggHatchRules.IsReadyToHatch(egg, data, heavyGravity);

            Assert.That(
                showsZeroDaysLeft,
                Is.EqualTo(hatchesNow),
                $"FedDayCount={fed}에서 표시와 부화 판정이 어긋납니다.");
        }
    }

    [Test]
    public void ResolveRemainingDays_AlreadyOverTarget_StopsAtZero()
    {
        BabyDragonData data = MakeData(BASE_DAYS_TO_HATCH);
        DragonEgg egg = new DragonEgg { FedDayCount = BASE_DAYS_TO_HATCH + 5 };

        Assert.That(
            DragonEggHatchRules.ResolveRemainingDays(egg, data, RunModifierSnapshot.Neutral),
            Is.EqualTo(0));
    }

    [Test]
    public void ResolveDaysToHatch_NullData_ReturnsZeroWithoutThrowing()
    {
        Assert.That(DragonEggHatchRules.ResolveDaysToHatch(null, RunModifierSnapshot.Neutral), Is.EqualTo(0));
        Assert.That(DragonEggHatchRules.IsReadyToHatch(null, null, RunModifierSnapshot.Neutral), Is.False);
    }

    private BabyDragonData MakeData(int daysToHatch)
    {
        BabyDragonData data = ScriptableObject.CreateInstance<BabyDragonData>();
        _created.Add(data);

        // _daysToHatch는 private [SerializeField]다 - 에셋 저작용 setter가 없으므로 테스트에서만 이렇게 채운다.
        SetPrivateInt(data, "_daysToHatch", daysToHatch);
        return data;
    }

    private static void SetPrivateInt(object target, string fieldName, int value)
    {
        System.Reflection.FieldInfo field = null;

        for (System.Type type = target.GetType(); type != null && field == null; type = type.BaseType)
        {
            field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        }

        Assert.That(field, Is.Not.Null, $"{fieldName} 필드를 찾지 못했습니다.");
        field.SetValue(target, value);
    }

    private RunModifierSnapshot SnapshotWithExtraHatchDays(int delta)
    {
        RunCounterEffectSO effect = ScriptableObject.CreateInstance<RunCounterEffectSO>();
        _created.Add(effect);
        effect.Configure(RunCounterChannel.EggHatchDays, delta);

        RunMutatorTier tier = new RunMutatorTier();
        tier.Configure(0, string.Empty, new RunMutatorEffectSO[] { effect });

        RunMutatorSO mutator = ScriptableObject.CreateInstance<RunMutatorSO>();
        _created.Add(mutator);
        mutator.Configure("heavy_gravity", string.Empty, string.Empty, new[] { tier });

        return RunModifierResolver.Resolve(
            new List<RunMutatorSelection> { new RunMutatorSelection(mutator, RunMutatorSO.FIRST_TIER) });
    }
}
