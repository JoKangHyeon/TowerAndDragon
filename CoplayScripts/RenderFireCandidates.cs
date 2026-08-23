using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 화염 명중 이펙트의 바탕이 될 원본 후보를 나란히 굽는다.
//
// 원본을 그대로 찍으면 안 된다. 이 팩의 바닥 데칼은 대부분 HorizontalBillboard(월드 XZ 평면 고정)라
// 회전 0인 이 프로젝트의 카메라에서 옆날로 서서 아무것도 그리지 않는다. 얼음·암석이 살아난 것도
// 이것을 Billboard로 바꾸고 세로만 눌러서였다. 변환 없이 찍으면 정작 후보를 살릴 자식들이
// 빈칸으로 나와, 쓸 만한 후보를 "아무것도 안 보인다"는 이유로 떨어뜨리게 된다.
//
// 그래서 씬 인스턴스에 한해 같은 변환을 걸고 찍는다. 색조와 HDR은 걸지 않는다 -
// 지금은 바탕을 고르는 단계이지 튜닝하는 단계가 아니다.
//
// 크기는 후보마다 맞추지 않고 전부 같은 배수로 찍은 뒤, 화면에서 잰 폭을 보고서에 숫자로 적는다.
// 시각마다 절정이 다른 이펙트를 한 시점 기준으로 자동 정규화하면, 느리게 퍼지는 연기가 큰 후보는
// 불꽃 심지가 안 보일 때까지 줄어든다 - 눈으로 못 보는 왜곡이라 숫자로 두는 편이 낫다.
//
// 프리팹 에셋은 건드리지 않는다. 씬에 만든 인스턴스에서만 바꿔 찍는다.
public static class RenderFireCandidates
{
    private const string SOURCE_FOLDER =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    private static readonly string[] CANDIDATES =
    {
        // 사용자가 지목한 셋 + 같은 계열의 나머지 하나
        "FX_RealisticEXP_B01",
        "FX_RealisticEXP_M01",
        "FX_RealisticEXP_S01",
        "FX_RealisticEXP_S02",

        // 얼음·암석이 골랐던 Crack_ 계열의 화염판. 바닥 접지가 있는 쪽이다.
        "FX_Crack_Firewave",
        "FX_Crack_Lava",
        "FX_Crack_Risingfire",
        "FX_Crack_EXP",

        // 이 프로젝트는 타일 아트가 아이소메트릭이라 만화풍이 오히려 맞을 수 있다.
        "FX_CartoonEXP_FireUP",

        // 자식 없는 단일 이미터. 접지 표현이 없을 가능성이 높지만 한 줄이니 같이 본다.
        "FX_Realistic_SpitFire"
    };

    private static readonly float[] TIMES = { 0.06f, 0.12f, 0.2f, 0.35f };

    // 얼음 0.05, 암석 0.08을 쓰고 있다. 같은 팩이라 authoring 기준이 비슷하므로 그 사이 값으로 둔다.
    private const float ROOT_SCALE = 0.08f;

    private const float ISO_SQUASH = 0.5f;

    private const int PANEL = 320;
    private const float ORTHO_SIZE = 2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    // 배경보다 확실히 밝은 픽셀만 그림으로 친다.
    private const float LUMINANCE_THRESHOLD = 0.12f;

    private static readonly Color BACKGROUND = new Color(0.06f, 0.07f, 0.09f, 1f);
    private static readonly Color SPAWN_LINE = new Color(0.45f, 0.45f, 0.5f, 1f);

    public static string Execute()
    {
        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * CANDIDATES.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("FireCandidateCamera");
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
            for (int row = 0; row < CANDIDATES.Length; row++)
            {
                string name = CANDIDATES[row];
                var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{SOURCE_FOLDER}/{name}.prefab");

                report.Append("== ").AppendLine(name);

                if (source == null)
                {
                    report.AppendLine("  원본을 찾을 수 없습니다");
                    continue;
                }

                int rowY = (CANDIDATES.Length - 1 - row) * PANEL;
                bool dumpedModes = false;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    GameObject instance = Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);
                    instance.transform.localScale *= ROOT_SCALE;

                    if (!dumpedModes)
                    {
                        DumpRenderModes(instance, report);
                        dumpedModes = true;
                    }

                    LayDecalsFlat(instance);

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

                    Object.DestroyImmediate(panel);
                    Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "fire_candidates.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            report.AppendLine();
            report.AppendLine(path);
            report.Append("줄(위→아래): ").AppendLine(string.Join(", ", CANDIDATES));
            report.Append("칸(왼→오른) 시각(초): ").AppendLine(string.Join(", ", TIMES));
            report.AppendLine($"전부 같은 배수 {ROOT_SCALE}로 찍었다. 칸 하나가 화면 {ORTHO_SIZE * 2}칸이다.");

            return report.ToString();
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

    // 어느 자식이 옆날로 서 있었는지 남긴다. 빈 칸이 "진짜 비었다"인지
    // "변환 전이면 안 보였을 것"인지 나중에 구분하려면 이 목록이 있어야 한다.
    private static void DumpRenderModes(GameObject instance, StringBuilder report)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null)
            {
                continue;
            }

            bool laidFlat = renderer.renderMode == ParticleSystemRenderMode.HorizontalBillboard;

            report.Append("   ").Append(ps.gameObject.name.PadRight(22));
            report.Append(renderer.renderMode.ToString().PadRight(20));
            report.AppendLine(laidFlat ? "<- 눕혀서 안 보이던 것" : "");
        }
    }

    // 얼음·암석에 건 것과 같은 변환. 월드 XZ 평면에 고정된 자식만 카메라를 보게 돌리고,
    // 아이소메트릭 셀이 1 x 0.5이므로 세로만 절반으로 눌러 바닥에 누운 타원으로 만든다.
    private static void LayDecalsFlat(GameObject instance)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null || renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard)
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

    // 화면에 실제로 그려진 폭·높이를 월드 단위로 잰다. 파티클 경계 상자는 빌보드 쿼드의 크기이지
    // 텍스처에 그려진 범위가 아니라서, 여백이 큰 데칼은 실제보다 몇 배 넓게 나온다.
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
        float width = (maxX - minX + 1) * perPixel;
        float height = (maxY - minY + 1) * perPixel;
        float bottom = (minY - PANEL * 0.5f) * perPixel;

        report.Append("  폭=").Append(width.ToString("0.##"));
        report.Append("  높이=").Append(height.ToString("0.##"));
        report.Append("  밑=").Append(bottom.ToString("0.##"));
        report.AppendLine($"  픽셀={lit}");
    }

    // 스폰 높이(0)에 가로선을 긋는다. 바닥에 닿아 보이는지 눈으로 재려면 기준선이 있어야 한다.
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
