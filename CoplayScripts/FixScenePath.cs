using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// 에디터 전용 일회성 스크립트 — 씬을 원래 경로에 저장하고 잘못 생성된 사본 제거
public static class FixScenePath
{
    public static string Execute()
    {
        Scene active = SceneManager.GetActiveScene();
        string before = active.path;
        bool saved = EditorSceneManager.SaveScene(active, "Assets/Scenes/SampleScene.unity");
        bool deleted = false;
        if (saved && AssetDatabase.AssetPathExists("Assets/SampleScene.unity"))
            deleted = AssetDatabase.DeleteAsset("Assets/SampleScene.unity");
        AssetDatabase.Refresh();
        return $"before={before} saved={saved} strayDeleted={deleted} now={SceneManager.GetActiveScene().path}";
    }
}
