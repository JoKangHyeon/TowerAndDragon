using System.IO;
using UnityEditor;
using UnityEngine;

// 지금 저장된 암석 프리팹에서 자식을 하나씩만 남기고 굽는다. 값을 바꾸지 않는다 -
// 이미 반영된 상태에서 "무엇이 금색으로 튀는가"를 찾는 용도다.
public static class IsolateStoneCurrent
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab";

    private static readonly string[] ISOLATED_NAMES =
    {
        "AOE", "floor_glow2", "shangmianglow", "decal", "floor_glowC",
        "glow", "ring", "glowdi", "glow_Center", "flame (3)"
    };

    private static readonly float[] TIMES = { 0.06f, 0.12f, 0.2f, 0.35f };

    private const int PANEL = 320;
    private const float ORTHO_SIZE = 2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);

        if (source == null)
        {
            return $"프리팹을 찾을 수 없습니다: {IMPACT_PATH}";
        }

        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * ISOLATED_NAMES.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("StoneCurrentCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        camera.allowHDR = true;

        var target = new RenderTexture(PANEL, PANEL, 24, RenderTextureFormat.DefaultHDR);

        try
        {
            for (int row = 0; row < ISOLATED_NAMES.Length; row++)
            {
                int rowY = (ISOLATED_NAMES.Length - 1 - row) * PANEL;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    var instance = Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                    foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var renderer = ps.GetComponent<ParticleSystemRenderer>();

                        if (renderer != null && ps.gameObject.name != ISOLATED_NAMES[row])
                        {
                            renderer.enabled = false;
                        }

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

                    sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

                    Object.DestroyImmediate(panel);
                    Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "stone_current_isolated.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            return $"{path}\n줄(위→아래): {string.Join(", ", ISOLATED_NAMES)}\n" +
                   $"칸(왼→오른) 시각(초): {string.Join(", ", TIMES)}";
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(sheet);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
