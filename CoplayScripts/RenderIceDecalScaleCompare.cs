using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 색을 3배로 고정한 채, 바닥 데칼만 키워 가며 굽는다.
//
// rock(솟는 얼음 창)은 건드리지 않는다 - 지금 크기가 셀에 맞게 맞춰져 있고,
// 키우면 지난번에 맞춘 밑동 정렬이 어긋난다. 주변 효과만 키운다.
//
// 프리팹 에셋은 건드리지 않는다 - 씬에 만든 인스턴스에서만 바꿔 찍는다.
public static class RenderIceDecalScaleCompare
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";

    // 크기를 키울 바닥 데칼 전부.
    private static readonly string[] SCALED_NAMES = { "glow", "nova", "blue_crack", "ground" };

    // 그중 가산합성이라 색을 올릴 수 있는 것들. ground는 URP_AlphaBlendFlow라 제외한다.
    private static readonly string[] BOOSTED_NAMES = { "glow", "nova", "blue_crack" };

    private static readonly float[] SIZE_MULTIPLIERS = { 1f, 1.5f, 2f, 3f };

    private const float COLOR_MULTIPLIER = 3f;

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

        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * SIZE_MULTIPLIERS.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("IceDecalScaleCamera");
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
            for (int row = 0; row < SIZE_MULTIPLIERS.Length; row++)
            {
                // 텍스처는 y가 위로 자라므로 첫 배수가 맨 윗줄에 오도록 뒤집는다.
                int rowY = (SIZE_MULTIPLIERS.Length - 1 - row) * PANEL;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    var instance = UnityEngine.Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                    Tune(instance, SIZE_MULTIPLIERS[row]);

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

                    UnityEngine.Object.DestroyImmediate(panel);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "ice_decal_scale.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            return $"{path}\n줄(위→아래) 크기 배수: {string.Join(", ", SIZE_MULTIPLIERS)}\n" +
                   $"칸(왼→오른) 시각(초): {string.Join(", ", TIMES)}\n" +
                   $"색은 {COLOR_MULTIPLIER}배 고정 / 대상: {string.Join(", ", SCALED_NAMES)} (rock은 그대로)";
        }
        finally
        {
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(sheet);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Tune(GameObject root, float sizeMultiplier)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            string name = ps.gameObject.name;
            var main = ps.main;

            if (Array.IndexOf(BOOSTED_NAMES, name) >= 0
                && main.startColor.mode == ParticleSystemGradientMode.Color)
            {
                Color source = main.startColor.color;

                // 알파는 그대로 둔다. 알파를 건드리면 페이드 아웃 타이밍이 같이 바뀐다.
                main.startColor = new Color(
                    source.r * COLOR_MULTIPLIER,
                    source.g * COLOR_MULTIPLIER,
                    source.b * COLOR_MULTIPLIER,
                    source.a);
            }

            if (Array.IndexOf(SCALED_NAMES, name) < 0)
            {
                continue;
            }

            ParticleSystem.MinMaxCurve width = main.startSizeX;
            ParticleSystem.MinMaxCurve height = main.startSizeY;
            ParticleSystem.MinMaxCurve depth = main.startSizeZ;

            main.startSizeX = ScaledCurve(width, sizeMultiplier);
            main.startSizeY = ScaledCurve(height, sizeMultiplier);
            main.startSizeZ = ScaledCurve(depth, sizeMultiplier);
        }
    }

    private static ParticleSystem.MinMaxCurve ScaledCurve(ParticleSystem.MinMaxCurve source, float multiplier)
    {
        switch (source.mode)
        {
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(
                    source.constantMin * multiplier, source.constantMax * multiplier);

            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curve);

            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curveMin, source.curveMax);

            default:
                return new ParticleSystem.MinMaxCurve(source.constant * multiplier);
        }
    }
}
