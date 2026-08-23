using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 화염 명중 후보를 얼음·암석 최종본과 같은 시트에 놓고 굽는다.
//
// B01 조합이 너무 화려하다는 판단이 나와, 조용한 쪽 둘을 본다.
//
// 1) FX_RealisticEXP_S01 - 같은 Realistic 계열의 가장 작은 폭발. 0.08에서는 t=0.35에
//    20픽셀까지 꺼져 버릴 만큼 작아서, 여기서는 배수를 크게 올려 본다.
// 2) FX_Crack_Risingfire의 불기둥(grad10) - 이전 버전에서 쓴 바탕의 일부다. 자식을 골라 켜서
//    "불기둥만", "불기둥 + 바닥 데칼"로 나눠 굽는다. 연기·별반짝임을 빼면 얼마나 조용해지는지 본다.
//
// 후보 줄에는 얼음·암석에 건 것과 같은 변환(HorizontalBillboard -> Billboard + 2:1 눌림)을
// 걸어 둔다. 이것 없이 찍으면 월드 XZ 평면에 고정된 바닥 데칼이 옆날로 서서 안 보인다.
//
// 프리팹 에셋은 건드리지 않는다. 씬에 만든 인스턴스에서만 끄고 켠다.
public static class RenderFireScaleCompare
{
    private const string PACK =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    private sealed class Row
    {
        public string Label;
        public string Path;
        public float Scale = 1f;
        public bool LaysDecalsFlat;

        // 비워 두면 전부 켠다. 채우면 그 이름들만 켜고 나머지 렌더러는 끈다.
        public string[] OnlyChildren;
    }

    private static readonly Row[] ROWS =
    {
        new Row
        {
            Label = "얼음 (현재)",
            Path = "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab"
        },
        new Row
        {
            Label = "암석 (현재)",
            Path = "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab"
        },
        new Row
        {
            Label = "화염 (Impact_Fire_V1 + Flash)",
            Path = "Assets/Imported/Prefabs/Effects/Status/FX_Impact_FireCrack.prefab"
        }
    };

    private static readonly float[] TIMES = { 0.06f, 0.12f, 0.25f, 0.5f, 0.9f };

    private const float ISO_SQUASH = 0.5f;

    private const int PANEL = 400;
    private const float ORTHO_SIZE = 1.2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    private const float LUMINANCE_THRESHOLD = 0.12f;

    private static readonly Color BACKGROUND = new Color(0.06f, 0.07f, 0.09f, 1f);
    private static readonly Color SPAWN_LINE = new Color(0.4f, 0.4f, 0.45f, 1f);

    public static string Execute()
    {
        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * ROWS.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("FireCompareCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BACKGROUND;
        camera.allowHDR = true;

        var target = new RenderTexture(PANEL, PANEL, 24, RenderTextureFormat.DefaultHDR);

        try
        {
            for (int row = 0; row < ROWS.Length; row++)
            {
                Row entry = ROWS[row];
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);

                report.Append("== ").AppendLine(entry.Label);

                if (source == null)
                {
                    report.AppendLine("  프리팹을 찾을 수 없습니다");
                    continue;
                }

                int rowY = (ROWS.Length - 1 - row) * PANEL;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);
                    instance.transform.localScale *= entry.Scale;

                    Prepare(instance, entry);

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

                    Measure(panel, TIMES[index], report);
                    DrawSpawnLine(panel);

                    sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

                    UnityEngine.Object.DestroyImmediate(panel);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "fire_scale_compare.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            var labels = new List<string>();

            foreach (Row entry in ROWS)
            {
                labels.Add(entry.Label);
            }

            report.AppendLine();
            report.AppendLine(path);
            report.Append("줄(위→아래): ").AppendLine(string.Join(", ", labels));
            report.Append("칸(왼→오른) 시각(초): ").AppendLine(string.Join(", ", TIMES));

            return report.ToString();
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

    private static void Prepare(GameObject instance, Row entry)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null)
            {
                continue;
            }

            if (entry.OnlyChildren != null && Array.IndexOf(entry.OnlyChildren, ps.gameObject.name) < 0)
            {
                renderer.enabled = false;
                continue;
            }

            if (!entry.LaysDecalsFlat || renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard)
            {
                continue;
            }

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;

            var main = ps.main;
            ParticleSystem.MinMaxCurve width = main.startSizeX;

            main.startSize3D = true;
            main.startSizeY = Scaled(width, ISO_SQUASH);
            main.startSizeZ = width;
        }
    }

    private static void Measure(Texture2D panel, float time, StringBuilder report)
    {
        Color[] pixels = panel.GetPixels();
        int minX = PANEL, maxX = -1, minY = PANEL, maxY = -1;
        int lit = 0;

        for (int y = 0; y < PANEL; y++)
        {
            for (int x = 0; x < PANEL; x++)
            {
                Color c = pixels[y * PANEL + x];

                if (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f <= LUMINANCE_THRESHOLD)
                {
                    continue;
                }

                lit++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        report.Append("  t=").Append(time.ToString("0.##"));

        if (lit == 0)
        {
            report.AppendLine("  (아무것도 그려지지 않음)");
            return;
        }

        float perPixel = ORTHO_SIZE * 2f / PANEL;

        report.Append("  폭=").Append(((maxX - minX + 1) * perPixel).ToString("0.##"));
        report.Append("  밑=").Append(((minY - PANEL * 0.5f) * perPixel).ToString("0.##"));
        report.Append("  위=").Append(((maxY - PANEL * 0.5f) * perPixel).ToString("0.##"));
        report.AppendLine("  픽셀=" + lit);
    }

    private static void DrawSpawnLine(Texture2D panel)
    {
        int y = PANEL / 2;

        for (int x = 0; x < PANEL; x++)
        {
            panel.SetPixel(x, y, SPAWN_LINE);
        }

        panel.Apply();
    }

    private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve source, float multiplier)
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
