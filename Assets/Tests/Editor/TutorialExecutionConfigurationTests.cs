using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TutorialExecutionConfigurationTests
{
    private const string TUTORIAL_SYSTEM_PREFAB_PATH =
        "Assets/Prefabs/Tutorial System.prefab";

    private const string CHAPTER_NAME_PREFIX = "Chapter_";
    private const string LEGACY_TIP_CHAIN_NAME_PREFIX = "TipChain_";
    private const string RETIRED_CONTROLLER_TYPE_NAME = "TutorialTipChainController";

    [Test]
    public void TutorialSystem_HasNoRetiredTipChainControllerOrMissingScripts()
    {
        GameObject prefab = LoadTutorialSystemPrefab();
        MonoBehaviour[] rootBehaviours = prefab.GetComponents<MonoBehaviour>();

        Assert.That(rootBehaviours, Has.None.Null, "튜토리얼 시스템 루트에 Missing Script가 있습니다.");
        Assert.That(
            rootBehaviours.Select(behaviour => behaviour.GetType().Name),
            Has.None.EqualTo(RETIRED_CONTROLLER_TYPE_NAME));
    }

    [Test]
    public void Scenario_ReferencesOnlyUniqueGateHoldingChapterRunners()
    {
        TutorialScenarioController scenario =
            LoadTutorialSystemPrefab().GetComponent<TutorialScenarioController>();
        Assert.That(scenario, Is.Not.Null);

        var scenarioData = new SerializedObject(scenario);
        SerializedProperty chapters = scenarioData.FindProperty("_chapters");
        var uniqueRunners = new HashSet<TutorialRunner>();

        Assert.That(chapters, Is.Not.Null);
        Assert.That(chapters.arraySize, Is.GreaterThan(0));

        for (int index = 0; index < chapters.arraySize; index++)
        {
            SerializedProperty entry = chapters.GetArrayElementAtIndex(index);
            TutorialRunner runner = entry.FindPropertyRelative("Runner").objectReferenceValue as TutorialRunner;

            Assert.That(runner, Is.Not.Null, $"{index}번째 챕터 러너가 비어 있습니다.");
            Assert.That(uniqueRunners.Add(runner), Is.True, $"{runner.name} 러너가 중복 등록됐습니다.");
            Assert.That(runner.name, Does.StartWith(CHAPTER_NAME_PREFIX));

            var runnerData = new SerializedObject(runner);
            Assert.That(runnerData.FindProperty("_holdsGates").boolValue, Is.True,
                $"{runner.name}이 튜토리얼 관문을 잡지 않습니다.");
        }
    }

    [Test]
    public void LegacyTipChainObjects_AreInactiveAndNotRegisteredAsChapters()
    {
        GameObject prefab = LoadTutorialSystemPrefab();
        TutorialScenarioController scenario = prefab.GetComponent<TutorialScenarioController>();
        var scenarioData = new SerializedObject(scenario);
        SerializedProperty chapters = scenarioData.FindProperty("_chapters");
        var chapterRunners = new HashSet<TutorialRunner>();

        for (int index = 0; index < chapters.arraySize; index++)
        {
            chapterRunners.Add(
                chapters.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("Runner")
                    .objectReferenceValue as TutorialRunner);
        }

        GameObject[] legacyObjects = prefab
            .GetComponentsInChildren<Transform>(true)
            .Select(transform => transform.gameObject)
            .Where(gameObject => gameObject.name.StartsWith(LEGACY_TIP_CHAIN_NAME_PREFIX))
            .ToArray();

        Assert.That(legacyObjects, Is.Not.Empty, "비교·전환용 레거시 팁 체인 에셋이 사라졌습니다.");
        Assert.That(legacyObjects, Has.All.Matches<GameObject>(gameObject => !gameObject.activeSelf));
        Assert.That(
            legacyObjects.Select(gameObject => gameObject.GetComponent<TutorialRunner>()),
            Has.None.Matches<TutorialRunner>(runner => chapterRunners.Contains(runner)));
    }

    [Test]
    public void ScenarioController_RunsBeforeChapterComponents()
    {
        DefaultExecutionOrder executionOrder = typeof(TutorialScenarioController)
            .GetCustomAttribute<DefaultExecutionOrder>();

        Assert.That(executionOrder, Is.Not.Null);
        Assert.That(executionOrder.order, Is.LessThan(0));
    }

    [Test]
    public void ObjectiveController_HasNoRetiredTipChainDependencies()
    {
        const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic;

        Assert.That(typeof(TutorialObjectiveController).GetField("_tipChainController", FLAGS), Is.Null);
        Assert.That(typeof(TutorialObjectiveController).GetField("_workerModeNudgeDelaySeconds", FLAGS), Is.Null);
    }

    private static GameObject LoadTutorialSystemPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TUTORIAL_SYSTEM_PREFAB_PATH);
        Assert.That(prefab, Is.Not.Null, $"프리팹을 찾지 못했습니다: {TUTORIAL_SYSTEM_PREFAB_PATH}");
        return prefab;
    }
}
