using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 얼음과 암석 명중 이펙트가 화면 어느 높이에 그려지는지 렌더된 픽셀로 잰다.
//
// 파티클 경계 상자로는 못 잰다 - 빌보드 쿼드의 크기이지 텍스처에 실제로 그려진 범위가 아니라서,
// 여백이 큰 데칼은 경계가 실제 그림보다 몇 배 넓게 나온다.
//
// 스폰 지점을 0으로 두고, 밝기가 기준을 넘는 픽셀의 위아래 끝과 무게중심을 월드 단위로 돌려준다.
// 음수가 아래쪽이다. 시트에는 스폰 높이에 빨간 선을 그어 눈으로도 비교할 수 있게 한다.
public static class RenderImpactAlign
{
    private static readonly string[] IMPACT_PATHS =
    {
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab",
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab",
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Fire.prefab"
    };

    private static readonly float[] TIMES = { 0.06f, 0.12f, 0.2f, 0.35f };

    private const int PANEL = 500;
    private const float ORTHO_SIZE = 2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    // 배경(0.06, 0.07, 0.09)보다 확실히 밝은 픽셀만 그림으로 친다.
    private const float LUMINANCE_THRESHOLD = 0.12f;

    public static string Execute()
    {
        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * IMPACT_PATHS.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("AlignCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        camera.allowHDR = true;

        var target = new RenderTexture(PANEL, PANEL, 24, RenderTextureFormat.DefaultHDR);

        // 픽셀 하나가 몇 월드 단위인지. 패널 세로가 직교 크기의 두 배다.
        float worldPerPixel = ORTHO_SIZE * 2f / PANEL;

        try
        {
            for (int row = 0; row < IMPACT_PATHS.Length; row++)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATHS[row]);

                if (source == null)
                {
                    report.AppendLine($"프리팹을 찾을 수 없습니다: {IMPACT_PATHS[row]}");
                    continue;
                }

                // 텍스처는 y가 위로 자라므로 첫 프리팹이 맨 윗줄에 오도록 뒤집는다.
                int rowY = (IMPACT_PATHS.Length - 1 - row) * PANEL;

                report.AppendLine($"== {Path.GetFileNameWithoutExtension(IMPACT_PATHS[row])}");

                for (int index = 0; index < TIMES.Length; index++)
                {
                    var instance = Object.Instantiate(source);
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

                    Color[] pixels = panel.GetPixels();
                    Measure(pixels, worldPerPixel, out float bottom, out float top, out float centroid, out int lit);

                    report.Append($"  t={TIMES[index]:0.##}  밑={bottom,7:0.###} 위={top,7:0.###}");
                    report.AppendLine($" 무게중심={centroid,7:0.###} 픽셀={lit}");

                    DrawSpawnLine(pixels);
                    sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, pixels);

                    Object.DestroyImmediate(panel);
                    Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "impact_align.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            report.Insert(0, $"{path}\n윗줄=얼음 / 아랫줄=암석, 빨간 선=스폰 높이(0)\n" +
                             $"시각(초): {string.Join(", ", TIMES)}\n");
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

    private static void Measure(
        Color[] pixels, float worldPerPixel, out float bottom, out float top, out float centroid, out int lit)
    {
        int lowestRow = int.MaxValue;
        int highestRow = int.MinValue;
        float weightSum = 0f;
        float weightedRowSum = 0f;
        lit = 0;

        for (int y = 0; y < PANEL; y++)
        {
            for (int x = 0; x < PANEL; x++)
            {
                Color pixel = pixels[y * PANEL + x];
                float luminance = pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f;

                if (luminance < LUMINANCE_THRESHOLD)
                {
                    continue;
                }

                lit++;
                weightSum += luminance;
                weightedRowSum += luminance * y;

                if (y < lowestRow)
                {
                    lowestRow = y;
                }

                if (y > highestRow)
                {
                    highestRow = y;
                }
            }
        }

        if (lit == 0)
        {
            bottom = 0f;
            top = 0f;
            centroid = 0f;
            return;
        }

        // 패널 한가운데가 스폰 높이다.
        float center = PANEL * 0.5f;
        bottom = (lowestRow - center) * worldPerPixel;
        top = (highestRow - center) * worldPerPixel;
        centroid = (weightedRowSum / weightSum - center) * worldPerPixel;
    }

    private static void DrawSpawnLine(Color[] pixels)
    {
        int row = PANEL / 2;
        var line = new Color(0.9f, 0.1f, 0.1f, 1f);

        for (int x = 0; x < PANEL; x++)
        {
            pixels[row * PANEL + x] = line;
        }
    }
}
