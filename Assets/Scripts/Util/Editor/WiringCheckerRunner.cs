using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>WiringChecker의 실행 진입점 - 메뉴, 콘솔 요약, 리포트 파일 출력을 담당한다.
/// 에디터 전용 도구라 CLAUDE.md 5번 예외(문자열·리터럴 상수 규칙 미적용)에 해당한다.</summary>
public static class WiringCheckerRunner
{
    private const string MENU_CHECK_BUILD_SCENES = "Tools/Wiring Checker/빌드 씬 전체 검사";
    private const string MENU_CHECK_ACTIVE_SCENE = "Tools/Wiring Checker/현재 씬만 검사";

    // Temp/는 .gitignore 대상이라 리포트가 커밋에 섞이지 않는다.
    private const string REPORT_DIRECTORY = "Temp";
    private const string REPORT_FILE_NAME = "WiringCheckReport.md";

    private const string PLAY_MODE_MESSAGE = "[WiringChecker] 플레이 모드에서는 검사할 수 없습니다. 정지 후 다시 실행하세요.";
    private const string MISSING_SCENE_FORMAT = "[WiringChecker] 빌드 목록의 씬 파일을 찾을 수 없습니다: {0}";

    public static string ReportPath => Path.Combine(REPORT_DIRECTORY, REPORT_FILE_NAME);

    [MenuItem(MENU_CHECK_BUILD_SCENES)]
    public static void CheckBuildScenesFromMenu()
    {
        Debug.Log(RunBuildSceneCheck());
    }

    [MenuItem(MENU_CHECK_ACTIVE_SCENE)]
    public static void CheckActiveSceneFromMenu()
    {
        if (IsBlockedByPlayMode())
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        List<WiringChecker.Issue> issues = WiringChecker.CheckScene(scene);

        // 열려 있는 씬이므로 콘솔 항목을 클릭하면 해당 오브젝트로 이동할 수 있게 개별 로그를 남긴다.
        foreach (WiringChecker.Issue issue in issues)
        {
            GameObject context = GameObject.Find(issue.ObjectPath);
            if (issue.Level == WiringChecker.Severity.Error)
            {
                Debug.LogError(issue.Describe(), context);
            }
            else
            {
                Debug.LogWarning(issue.Describe(), context);
            }
        }

        // 이 경로는 리포트 파일을 쓰지 않는다 - 콘솔의 개별 로그가 결과다.
        Debug.Log(BuildSummary(new List<string> { scene.path }, issues, false));
    }

    /// <summary>빌드 씬 전체를 검사하고 리포트를 파일로 남긴 뒤, 콘솔·호출부에 줄 요약 문자열을 돌려준다.
    /// Coplay execute_script 등 외부 도구에서 이 메서드를 직접 호출한다.</summary>
    public static string RunBuildSceneCheck()
    {
        if (IsBlockedByPlayMode())
        {
            return PLAY_MODE_MESSAGE;
        }

        var scannedPaths = new List<string>();
        var issues = new List<WiringChecker.Issue>();

        foreach (string path in WiringChecker.CollectBuildScenePaths())
        {
            if (!File.Exists(path))
            {
                Debug.LogError(string.Format(MISSING_SCENE_FORMAT, path));
                continue;
            }

            scannedPaths.Add(path);
            issues.AddRange(WiringChecker.CheckSceneAtPath(path));
        }

        WriteReport(scannedPaths, issues);
        return BuildSummary(scannedPaths, issues, true);
    }

    private static bool IsBlockedByPlayMode()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return false;
        }

        Debug.LogError(PLAY_MODE_MESSAGE);
        return true;
    }

    private static string BuildSummary(
        List<string> scenePaths, List<WiringChecker.Issue> issues, bool includeReportPath)
    {
        int errors = CountBySeverity(issues, WiringChecker.Severity.Error);
        var summary = new StringBuilder();
        summary.AppendLine($"[WiringChecker] 씬 {scenePaths.Count}개 검사 - 에러 {errors}건, 경고 {issues.Count - errors}건");

        foreach (string path in scenePaths)
        {
            int sceneErrors = 0;
            int sceneWarnings = 0;
            foreach (WiringChecker.Issue issue in issues)
            {
                if (issue.ScenePath != path)
                {
                    continue;
                }

                if (issue.Level == WiringChecker.Severity.Error)
                {
                    sceneErrors++;
                }
                else
                {
                    sceneWarnings++;
                }
            }

            summary.AppendLine($"  {path} - 에러 {sceneErrors}, 경고 {sceneWarnings}");
        }

        if (includeReportPath)
        {
            summary.Append($"리포트: {ReportPath}");
        }

        return summary.ToString().TrimEnd();
    }

    private static int CountBySeverity(List<WiringChecker.Issue> issues, WiringChecker.Severity level)
    {
        int count = 0;
        foreach (WiringChecker.Issue issue in issues)
        {
            if (issue.Level == level)
            {
                count++;
            }
        }

        return count;
    }

    private static void WriteReport(List<string> scenePaths, List<WiringChecker.Issue> issues)
    {
        var report = new StringBuilder();
        report.AppendLine("# 와이어링 검사 리포트");
        report.AppendLine();
        report.AppendLine("빌드 설정에 포함된 씬을 순회하며 인스펙터 참조 누락을 찾은 결과입니다.");
        report.AppendLine("비워둬도 되는 필드는 선언부에 `[WiringOptional]`을 붙이면 경고로 내려갑니다.");
        report.AppendLine();

        foreach (string path in scenePaths)
        {
            report.AppendLine($"## {path}");
            report.AppendLine();
            AppendSection(report, issues, path, WiringChecker.Severity.Error);
            AppendSection(report, issues, path, WiringChecker.Severity.Warning);
        }

        Directory.CreateDirectory(REPORT_DIRECTORY);
        File.WriteAllText(ReportPath, report.ToString());
    }

    private static void AppendSection(
        StringBuilder report,
        List<WiringChecker.Issue> issues,
        string scenePath,
        WiringChecker.Severity level)
    {
        var lines = new List<string>();
        foreach (WiringChecker.Issue issue in issues)
        {
            if (issue.ScenePath == scenePath && issue.Level == level)
            {
                lines.Add(issue.Describe());
            }
        }

        report.AppendLine(level == WiringChecker.Severity.Error ? "### 에러" : "### 경고 (WiringOptional)");
        report.AppendLine();

        if (lines.Count == 0)
        {
            report.AppendLine("없음");
            report.AppendLine();
            return;
        }

        foreach (string line in lines)
        {
            report.AppendLine("- " + line);
        }

        report.AppendLine();
    }
}
