using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 플레이 테스트 전 점검. 렌더 스크립트가 씬에 임시로 만든 오브젝트를 지우지 못한 채 끝났는지,
// 씬이 더럽혀졌는지 본다. 렌더는 STAGE_Y = -200에 인스턴스를 만들었다 지우는 방식이라
// 예외가 났다면 그 자리에 남아 있다.
public static class CheckPlayReady
{
    private const float STAGE_Y = -200f;
    private const float SEARCH_RADIUS = 50f;

    private static readonly string[] SUSPECT_PREFIXES =
    {
        "Impact_Tower_", "FX_Impact_", "IceImpactPreview", "IceDecal", "IceHdr", "IceCandidate", "StoneCandidate", "StoneIsolate"
    };

    public static string Execute()
    {
        var report = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        report.AppendLine($"열린 씬: {scene.path}");
        report.AppendLine($"저장 안 된 변경: {(scene.isDirty ? "있음" : "없음")}");
        report.AppendLine($"플레이 모드: {(EditorApplication.isPlaying ? "실행 중" : "정지")}");

        int leftovers = 0;

        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.transform.parent != null)
            {
                continue;
            }

            bool suspectName = false;

            foreach (string prefix in SUSPECT_PREFIXES)
            {
                if (go.name.StartsWith(prefix))
                {
                    suspectName = true;
                    break;
                }
            }

            bool atStage = Mathf.Abs(go.transform.position.y - STAGE_Y) < SEARCH_RADIUS;

            if (suspectName || atStage)
            {
                report.AppendLine($"  잔여 의심: {go.name} at {go.transform.position}");
                leftovers++;
            }
        }

        report.AppendLine(leftovers == 0 ? "잔여물 없음" : $"잔여물 {leftovers}개 - 지워야 한다");

        return report.ToString();
    }
}
