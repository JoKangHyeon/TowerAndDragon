using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 프리팹의 자식을 하나씩만 켜고 구워서, 화면에 실제로 그려진 크기를 자식별로 잰다.
//
// 두 가지를 특정하려고 만들었다.
//
// 1) FX_Crack_Risingfire의 어느 자식이 위로 솟는 불기둥인가.
//    루트 배수를 올려 폭을 얼음·암석에 맞추면 세로가 두 배로 튀는데, 어느 자식을 줄여야
//    하는지는 이름으로 알 수 없다(grad10이 둘, flare, gamestar 중 무엇인지).
//    "위" 값이 큰 자식이 범인이다.
//
// 2) 명중 순간(t=0.06)을 채워 줄 섬광으로 무엇을 얹을 것인가.
//    Risingfire는 자식 열 개가 전부 연속 방출이라 첫 프레임이 비는데, 그 구멍을 메울
//    버스트 자식을 다른 원본에서 골라 와야 한다. t=0.06 픽셀이 큰 자식이 후보다.
//
// 파티클 경계 상자로는 못 잰다 - 빌보드 쿼드의 크기이지 텍스처에 그려진 범위가 아니다.
// 그래서 렌더한 픽셀에서 잰다. 이미지는 남기지 않는다 - 숫자만 있으면 되는 일이다.
//
// 프리팹 에셋은 건드리지 않는다. 씬에 만든 인스턴스에서만 끄고 켠다.
public static class MeasureImpactChildren
{
    private const string SOURCE_FOLDER =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    private sealed class Target
    {
        // 팩 원본은 이름만 적으면 되고, 생성기 산출물은 전체 경로를 적는다.
        public string Name;
        public string FullPath;
        public float Scale = 1f;

        public string Path => FullPath ?? $"{SOURCE_FOLDER}/{Name}.prefab";
    }

    private static readonly Target[] TARGETS =
    {
        // 이 작업 전까지 화염 타워가 쓰던 임팩트. 소용돌이치는 불덩이와 노란 스파크가 섞여 있는데,
        // 어느 자식이 어느 쪽인지 이름(Derbis·Sparks·BigSparks·GlowBlack)으로는 알 수 없다.
        new Target
        {
            Name = "Impact_Fire_V1",
            FullPath = "Assets/Imported/Prefabs/Projectile/Impact_Fire_V1.prefab"
        },

        // 바닥 효과 후보. 불기둥을 뺀 나머지 넷(Flash·flame·glow·pulse)이 붉은 불씨 원반을 만든다.
        new Target { Name = "FX_Crack_Risingfire", Scale = 0.11f }
    };

    // 명중 순간·절정·꼬리. 세 시점이면 "즉시 터지는가", "얼마나 크는가", "제때 꺼지는가"가 갈린다.
    private static readonly float[] TIMES = { 0.12f, 0.3f, 0.6f };

    private const float ISO_SQUASH = 0.5f;

    private const int PANEL = 400;
    private const float ORTHO_SIZE = 1.2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    private const float LUMINANCE_THRESHOLD = 0.12f;

    private static readonly Color BACKGROUND = new Color(0.06f, 0.07f, 0.09f, 1f);

    public static string Execute()
    {
        var report = new StringBuilder();

        var cameraObject = new GameObject("ChildMeasureCamera");
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
            foreach (Target entry in TARGETS)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);

                report.Append("== ").Append(entry.Name).Append("  x").AppendLine(entry.Scale.ToString());

                if (source == null)
                {
                    report.AppendLine("  원본을 찾을 수 없습니다");
                    continue;
                }

                report.Append("  ").Append("자식".PadRight(24));

                foreach (float time in TIMES)
                {
                    report.Append($"t={time:0.##}".PadRight(38));
                }

                report.AppendLine();

                string[] names = ChildNames(source);

                foreach (string name in names)
                {
                    report.Append("  ").Append(name.PadRight(24));

                    foreach (float time in TIMES)
                    {
                        report.Append(MeasureOne(source, entry.Scale, name, time, camera, target).PadRight(38));
                    }

                    report.AppendLine();
                }

                report.AppendLine();
            }

            report.AppendLine("위=화면에서 가장 높이 그려진 지점(스폰 높이 0 기준), 픽셀=그려진 밝은 픽셀 수");

            return report.ToString();
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(cameraObject);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    private static string[] ChildNames(GameObject source)
    {
        ParticleSystem[] systems = source.GetComponentsInChildren<ParticleSystem>(true);
        var names = new string[systems.Length];

        for (int i = 0; i < systems.Length; i++)
        {
            names[i] = systems[i].gameObject.name;
        }

        return names;
    }

    // 이름이 같은 자식이 둘 있으면(Risingfire의 grad10, Crack_EXP의 ground) 둘 다 켜진다.
    // 이 조사에서는 그래도 된다 - 같은 이름이면 ChildScales도 어차피 둘 다 잡는다.
    private static string MeasureOne(
        GameObject source, float scale, string isolated, float time, Camera camera, RenderTexture target)
    {
        GameObject instance = Object.Instantiate(source);
        instance.transform.position = new Vector3(0f, STAGE_Y, 0f);
        instance.transform.localScale *= scale;

        try
        {
            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();

                if (renderer == null)
                {
                    continue;
                }

                if (ps.gameObject.name != isolated)
                {
                    renderer.enabled = false;
                    continue;
                }

                LayFlat(ps, renderer);
            }

            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Simulate(time, false, true, true);
            }

            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            var panel = new Texture2D(PANEL, PANEL, TextureFormat.RGBA32, false);
            panel.ReadPixels(new Rect(0f, 0f, PANEL, PANEL), 0, 0);
            panel.Apply();

            RenderTexture.active = previous;

            string measured = Describe(panel);

            Object.DestroyImmediate(panel);

            return measured;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // 얼음·암석에 건 것과 같은 변환. 이것 없이 재면 월드 XZ 평면에 고정된 자식이
    // 옆날로 서서 0으로 나온다.
    private static void LayFlat(ParticleSystem ps, ParticleSystemRenderer renderer)
    {
        if (renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard)
        {
            return;
        }

        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;

        var main = ps.main;
        ParticleSystem.MinMaxCurve width = main.startSizeX;

        main.startSize3D = true;
        main.startSizeY = Scaled(width, ISO_SQUASH);
        main.startSizeZ = width;
    }

    private static string Describe(Texture2D panel)
    {
        Color[] pixels = panel.GetPixels();
        int minX = PANEL, maxX = -1, maxY = -1;
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
                if (y > maxY) maxY = y;
            }
        }

        if (lit == 0)
        {
            return "-";
        }

        float perPixel = ORTHO_SIZE * 2f / PANEL;
        float width = (maxX - minX + 1) * perPixel;
        float top = (maxY - PANEL * 0.5f) * perPixel;

        return $"폭={width:0.##} 위={top:0.##} 픽셀={lit}";
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
