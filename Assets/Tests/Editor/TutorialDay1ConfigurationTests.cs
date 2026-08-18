using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TutorialDay1ConfigurationTests
{
    private const string INTRO_SEQUENCE_PATH =
        "Assets/Data/Tutorial/TS_Day1IntroSequence.asset";

    private const string BUILD_SEQUENCE_PATH =
        "Assets/Data/Tutorial/TS_Day1BuildSequence.asset";

    private const string DAY_ONE_WAVE_PATH =
        "Assets/Data/WaveData/TutorialWave/TW_Day1.asset";

    private const string TUTORIAL_SYSTEM_PREFAB_PATH =
        "Assets/Prefabs/Tutorial System.prefab";

    private static readonly string[] EXPECTED_INTRO_STEP_IDS =
    {
        "day1_intro",
        "day1_monster_path",
        "day1_objective_intro",
    };

    private static readonly string[] EXPECTED_BUILD_STEP_IDS =
    {
        "day1_open_build_mode",
        "day1_build_tab",
        "day1_select_tower",
        "day1_place_tower",
        "day1_cost_info",
        "day1_close_build_panel",
        "day1_click_tower",
        "day1_assign_population",
        "day1_operation_rate",
        "day1_assign_all",
        "day1_close_population_panel",
        "day1_tower_count",
        "day1_wait_egg_check",
        "day1_ready_for_night",
    };

    [Test]
    public void IntroSequence_UsesThreeConfirmedStepsInOrder()
    {
        TutorialSequenceSO sequence = LoadAsset<TutorialSequenceSO>(INTRO_SEQUENCE_PATH);

        Assert.That(
            sequence.Steps.Select(step => step.StepId),
            Is.EqualTo(EXPECTED_INTRO_STEP_IDS));
    }

    [Test]
    public void BuildSequence_ExcludesWorkerModeAndKeepsConfirmedOrder()
    {
        TutorialSequenceSO sequence = LoadAsset<TutorialSequenceSO>(BUILD_SEQUENCE_PATH);

        Assert.That(
            sequence.Steps.Select(step => step.StepId),
            Is.EqualTo(EXPECTED_BUILD_STEP_IDS));
        Assert.That(
            sequence.Steps.Any(step => step.StepId.Contains("worker")),
            Is.False,
            "일꾼 모드 안내는 2일차 마일스톤에서 연결해야 합니다.");
    }

    [Test]
    public void TowerGate_RequiresThreeFullyStaffedTowers()
    {
        TutorialSequenceSO sequence = LoadAsset<TutorialSequenceSO>(BUILD_SEQUENCE_PATH);
        TutorialStepSO gate = sequence.Steps.Single(step => step.StepId == "day1_tower_count");

        Assert.That(gate.Condition, Is.EqualTo(TutorialConditionType.BuildingCountReached));
        Assert.That(gate.TargetBuilding, Is.EqualTo(TutorialBuildingKind.AnyTower));
        Assert.That(gate.RequiredCount, Is.EqualTo(3));
        Assert.That(gate.RequiresStaffed, Is.True);
    }

    [Test]
    public void ReadyForNight_WaitsUntilBabyDragonEggGuideIsFinished()
    {
        TutorialSequenceSO sequence = LoadAsset<TutorialSequenceSO>(BUILD_SEQUENCE_PATH);
        TutorialStepSO eggCheck = sequence.Steps.Single(step => step.StepId == "day1_wait_egg_check");
        var steps = sequence.Steps.ToList();

        Assert.That(eggCheck.Condition, Is.EqualTo(TutorialConditionType.BabyDragonEggChecked));
        Assert.That(eggCheck.TargetMode, Is.EqualTo(TutorialExclusiveModeKind.BabyDragonInventory));
        Assert.That(
            steps.IndexOf(eggCheck),
            Is.LessThan(steps.FindIndex(step => step.StepId == "day1_ready_for_night")));
    }

    [Test]
    public void EggGrant_IsTriggeredByTowerGateOnBuildRunner()
    {
        GameObject prefab = LoadAsset<GameObject>(TUTORIAL_SYSTEM_PREFAB_PATH);
        BabyDragonTutorialHandOff handOff = prefab.GetComponent<BabyDragonTutorialHandOff>();
        var handOffData = new SerializedObject(handOff);
        TutorialStepSO grantAfterStep =
            handOffData.FindProperty("_grantAfterStep").objectReferenceValue as TutorialStepSO;
        TutorialRunner runner =
            handOffData.FindProperty("_runner").objectReferenceValue as TutorialRunner;
        var runnerData = new SerializedObject(runner);
        TutorialSequenceSO sequence =
            runnerData.FindProperty("_sequence").objectReferenceValue as TutorialSequenceSO;

        Assert.That(grantAfterStep, Is.Not.Null);
        Assert.That(grantAfterStep.StepId, Is.EqualTo("day1_tower_count"));
        Assert.That(sequence, Is.SameAs(LoadAsset<TutorialSequenceSO>(BUILD_SEQUENCE_PATH)));
    }

    [Test]
    public void EggCheckWait_AllowsDragonWindowTabShortcutToClose()
    {
        var runnerObject = new GameObject("TutorialRunner_Test");
        var dragonWindowObject = new GameObject("DragonWindow_Test");

        try
        {
            TutorialRunner runner = runnerObject.AddComponent<TutorialRunner>();
            UI_DragonWindow dragonWindow = dragonWindowObject.AddComponent<UI_DragonWindow>();
            TutorialStepSO eggCheck = LoadAsset<TutorialSequenceSO>(BUILD_SEQUENCE_PATH)
                .Steps.Single(step => step.StepId == "day1_wait_egg_check");

            SetPrivateField(runner, "_isRunning", true);
            SetPrivateField(runner, "_activeStep", eggCheck);

            Assert.That(((IShortcutBlockQuery)runner).AllowsShortcut(dragonWindow), Is.True);
            Assert.That(((IExclusiveModeOpenQuery)runner).CanClose(dragonWindow), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(dragonWindowObject);
            Object.DestroyImmediate(runnerObject);
        }
    }

    [Test]
    public void DayOneWave_UsesOnePortalAndFourMonsters()
    {
        WaveDefinitionSO wave = LoadAsset<WaveDefinitionSO>(DAY_ONE_WAVE_PATH);

        Assert.That(wave.PortalWaves, Has.Count.EqualTo(1));
        Assert.That(
            wave.PortalWaves
                .SelectMany(portal => portal.RouteWaves)
                .SelectMany(route => route.SpawnGroups)
                .Sum(group => group.SpawnCount),
            Is.EqualTo(4));
    }

    private static T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, $"에셋을 찾지 못했습니다: {path}");
        return asset;
    }

    private static void SetPrivateField<T>(TutorialRunner runner, string fieldName, T value)
    {
        FieldInfo field = typeof(TutorialRunner).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(field, Is.Not.Null, $"필드를 찾지 못했습니다: {fieldName}");
        field.SetValue(runner, value);
    }
}
