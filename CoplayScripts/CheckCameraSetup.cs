using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CheckCameraSetup
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
            {
                sb.AppendLine($"{camera.name}: ortho={camera.orthographic} size={camera.orthographicSize:0.##} " +
                              $"rot={camera.transform.eulerAngles:0} pos={camera.transform.position:0.#}");
            }
        }

        EditorSceneManager.CloseScene(scene, true);
        return sb.ToString();
    }
}
