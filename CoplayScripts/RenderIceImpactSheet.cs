using System.IO;
using UnityEditor;
using UnityEngine;

// 얼음 명중 이펙트를 여러 시각으로 시뮬레이트해 한 장의 대조표(PNG)로 굽는다.
//
// 씬에 놓고 capture_scene_object로 찍는 방법은 못 쓴다 - 에디트 모드에서는 Simulate로 맞춘
// 상태가 다음 리페인트에 풀려서, 캡처에는 아무것도 안 찍힌다.
// 시뮬레이트와 렌더를 같은 호출 안에서 끝내야 한다.
public static class RenderIceImpactSheet
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";

    private const string PREVIEW_NAME = "IceImpactPreview";

    private static readonly float[] TIMES = { 0.06f, 0.14f, 0.22f, 0.4f };

    private const int PANEL = 480;
    private const float ORTHO_SIZE = 1.3f;
    private const float CAMERA_DISTANCE = 10f;

    // 씬 한복판(원점)에는 성과 용이 있어 배경이 지저분하다. 아무것도 없는 곳으로 옮겨 찍는다.
    private const float STAGE_Y = -200f;

    public static string Execute()
    {
        CleanUp();

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);

        if (source == null)
        {
            return $"프리팹을 찾을 수 없습니다: {IMPACT_PATH}";
        }

        int width = PANEL * TIMES.Length;
        var sheet = new Texture2D(width, PANEL, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("IceImpactPreviewCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);

        var target = new RenderTexture(PANEL, PANEL, 24);

        try
        {
            for (int index = 0; index < TIMES.Length; index++)
            {
                var instance = Object.Instantiate(source);
                instance.name = PREVIEW_NAME;
                instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Simulate(TIMES[index], false, true, true);
                }

                camera.targetTexture = target;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;

                var panel = new Texture2D(PANEL, PANEL, TextureFormat.RGBA32, false);
                panel.ReadPixels(new Rect(0f, 0f, PANEL, PANEL), 0, 0);
                panel.Apply();

                RenderTexture.active = previous;

                sheet.SetPixels(index * PANEL, 0, PANEL, PANEL, panel.GetPixels());

                Object.DestroyImmediate(panel);
                Object.DestroyImmediate(instance);
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "ice_impact_sheet.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            return $"{path}\n패널 순서(초): {string.Join(", ", TIMES)}";
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(sheet);
            target.Release();
            Object.DestroyImmediate(target);
            CleanUp();
        }
    }

    private static void CleanUp()
    {
        GameObject leftover = GameObject.Find(PREVIEW_NAME);

        while (leftover != null)
        {
            Object.DestroyImmediate(leftover);
            leftover = GameObject.Find(PREVIEW_NAME);
        }
    }
}
