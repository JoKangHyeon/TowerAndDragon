using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// 에디터 전용 일회성 스크립트 — save_scene이 Assets/ 루트에 잘못 만든 사본을 정리하고
// 씬을 원래 경로에 저장한다. 특정 씬 이름에 하드코딩하지 않는다 — 어떤 씬이든 겪는 문제이므로
// 실행할 때마다 이 파일을 새로 쓰지 말고 realPath 인자만 바꿔서 재사용할 것.
//
// 호출 예: execute_script(filePath: "CoplayScripts/FixScenePath.cs", methodName: "Execute",
//           arguments: "{\"realPath\": \"Assets/Scenes/StartScene.unity\"}")
public static class FixScenePath
{
    public static string Execute(JObject args)
    {
        string realPath = args["realPath"]?.ToString();
        if (string.IsNullOrEmpty(realPath))
        {
            return "실패: arguments에 realPath(예: \"Assets/Scenes/StartScene.unity\")를 지정하세요.";
        }

        Scene active = SceneManager.GetActiveScene();
        string before = active.path;
        bool saved = EditorSceneManager.SaveScene(active, realPath);

        string strayPath = "Assets/" + Path.GetFileName(realPath);
        bool deleted = false;
        if (saved && strayPath != realPath && AssetDatabase.AssetPathExists(strayPath))
            deleted = AssetDatabase.DeleteAsset(strayPath);

        AssetDatabase.Refresh();
        return $"before={before} saved={saved} strayDeleted={deleted} now={SceneManager.GetActiveScene().path}";
    }
}
