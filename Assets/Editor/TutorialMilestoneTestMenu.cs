using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public static class TutorialMilestoneTestMenu
{
    private const string DAY_ONE_TEST_GROUP = "^TutorialDay1ConfigurationTests";
    private const string EXECUTION_TEST_GROUP = "^TutorialExecutionConfigurationTests";
    private const string MILESTONE_ONE_LABEL = "M1";
    private const string MILESTONE_TWO_LABEL = "M2";

    [MenuItem("Tests/Tutorial/Run M1 Day1 Configuration")]
    public static void RunDayOneConfigurationTests()
    {
        RunTests(DAY_ONE_TEST_GROUP, MILESTONE_ONE_LABEL);
    }

    [MenuItem("Tests/Tutorial/Run M2 Execution Configuration")]
    public static void RunExecutionConfigurationTests()
    {
        RunTests(EXECUTION_TEST_GROUP, MILESTONE_TWO_LABEL);
    }

    private static void RunTests(string testGroup, string milestoneLabel)
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        var callbacks = new MilestoneTestCallbacks(api, milestoneLabel);
        api.RegisterCallbacks(callbacks);
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = TestMode.EditMode,
            groupNames = new[] { testGroup },
        }));
    }

    private sealed class MilestoneTestCallbacks : ICallbacks
    {
        private readonly TestRunnerApi _api;
        private readonly string _milestoneLabel;

        public MilestoneTestCallbacks(TestRunnerApi api, string milestoneLabel)
        {
            _api = api;
            _milestoneLabel = milestoneLabel;
        }

        public void RunStarted(ITestAdaptor testsToRun)
        {
            Debug.Log($"[Tutorial {_milestoneLabel} Test] 실행 시작");
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            Debug.Log(
                $"[Tutorial {_milestoneLabel} Test] 실행 완료: {result.TestStatus}, " +
                $"통과 {result.PassCount}, 실패 {result.FailCount}, 생략 {result.SkipCount}");

            _api.UnregisterCallbacks(this);
            Object.DestroyImmediate(_api);
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
            {
                Debug.LogError(
                    $"[Tutorial {_milestoneLabel} Test] 실패: {result.FullName}\n{result.Message}");
            }
        }
    }
}
