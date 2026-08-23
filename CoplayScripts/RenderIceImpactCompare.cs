using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 얼음 명중 이펙트의 "빛 파열 추가 전/후"를 한 장으로 굽는다.
//
// 전(前)은 따로 보관해 둔 프리팹이 아니라, 현재 산출물에서 얹어 온 자식만 지워 만든다 -
// 생성기를 되돌렸다 다시 돌리지 않고도 같은 조건에서 비교할 수 있다.
public static class RenderIceImpactCompare
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";

    // BuildStatusVfx가 FX_Crack_BlueShock·FX_Crack_Blue02에서 얹은 자식들.
    private static readonly string[] ADDED_CHILDREN =
    {
        "BurstPlane1", "BurstPlane2", "BurstPlane3", "BurstRing", "BurstStreaks", "BurstFlare"
    };

    private static readonly float[] TIMES = { 0.04f, 0.08f, 0.14f, 0.22f, 0.4f };

    private const int PANEL = 420;
    private const float ORTHO_SIZE = 1.3f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);

        if (source == null)
        {
            return $"프리팹을 찾을 수 없습니다: {IMPACT_PATH}";
        }

        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * 2, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("IceCompareCamera");
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
            // 위 줄이 전(前), 아래 줄이 후(後). 텍스처는 y가 위로 자라므로 위 줄의 y가 PANEL이다.
            RenderRow(sheet, camera, target, source, true, PANEL);
            RenderRow(sheet, camera, target, source, false, 0);

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "ice_impact_compare.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            return $"{path}\n위 줄=추가 전 / 아래 줄=추가 후, 시각(초): {string.Join(", ", TIMES)}";
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

    private static void RenderRow(
        Texture2D sheet,
        Camera camera,
        RenderTexture target,
        GameObject source,
        bool removesAdded,
        int rowY)
    {
        for (int index = 0; index < TIMES.Length; index++)
        {
            var instance = Object.Instantiate(source);
            instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

            if (removesAdded)
            {
                RemoveAdded(instance);
            }

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

            sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

            Object.DestroyImmediate(panel);
            Object.DestroyImmediate(instance);
        }
    }

    private static void RemoveAdded(GameObject root)
    {
        var doomed = new List<GameObject>();

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in ADDED_CHILDREN)
            {
                if (child.name == name)
                {
                    doomed.Add(child.gameObject);
                }
            }
        }

        foreach (GameObject target in doomed)
        {
            Object.DestroyImmediate(target);
        }
    }
}
