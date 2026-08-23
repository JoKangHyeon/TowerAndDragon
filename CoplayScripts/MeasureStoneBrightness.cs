using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 지금 저장된 암석 프리팹에서 자식을 하나씩만 켜고 렌더해, 화면에 실제로 뿌리는 빛의 양을 잰다.
//
// 눈으로 시트를 보는 것으로는 "무엇이 금색을 만드는가"를 못 가른다 - 겹쳐 그려지면
// 밝은 것 하나가 나머지를 다 덮어 버려서, 지목이 틀려도 그럴듯해 보인다.
//
// 밝기 합(빛의 총량)으로 줄을 세우고, 평균 색으로 금색인지 본다.
// 값을 바꾸지 않는다 - 현재 상태를 그대로 잰다.
public static class MeasureStoneBrightness
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab";

    private static readonly float[] TIMES = { 0.2f, 0.35f };

    private const int PANEL = 300;
    private const float ORTHO_SIZE = 2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    private const float LUMINANCE_THRESHOLD = 0.12f;

    // 배경색. 빼고 재야 순수하게 그 자식이 더한 빛만 남는다.
    private static readonly Color BACKGROUND = new Color(0.06f, 0.07f, 0.09f, 1f);

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);

        if (source == null)
        {
            return $"프리팹을 찾을 수 없습니다: {IMPACT_PATH}";
        }

        var names = new List<string>();
        var probe = Object.Instantiate(source);

        foreach (ParticleSystem ps in probe.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!names.Contains(ps.gameObject.name))
            {
                names.Add(ps.gameObject.name);
            }
        }

        Object.DestroyImmediate(probe);

        var cameraObject = new GameObject("StoneBrightnessCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BACKGROUND;
        camera.allowHDR = true;

        var target = new RenderTexture(PANEL, PANEL, 24, RenderTextureFormat.DefaultHDR);
        var rows = new List<(float Total, string Line)>();

        try
        {
            foreach (string name in names)
            {
                float total = 0f;
                int lit = 0;
                float sumR = 0f;
                float sumG = 0f;
                float sumB = 0f;

                foreach (float time in TIMES)
                {
                    var instance = Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                    foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var renderer = ps.GetComponent<ParticleSystemRenderer>();

                        if (renderer != null && ps.gameObject.name != name)
                        {
                            renderer.enabled = false;
                        }

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

                    foreach (Color pixel in panel.GetPixels())
                    {
                        float r = pixel.r - BACKGROUND.r;
                        float g = pixel.g - BACKGROUND.g;
                        float b = pixel.b - BACKGROUND.b;
                        float luminance = r * 0.299f + g * 0.587f + b * 0.114f;

                        if (luminance < LUMINANCE_THRESHOLD)
                        {
                            continue;
                        }

                        total += luminance;
                        lit++;
                        sumR += r;
                        sumG += g;
                        sumB += b;
                    }

                    Object.DestroyImmediate(panel);
                    Object.DestroyImmediate(instance);
                }

                if (lit == 0)
                {
                    rows.Add((0f, $"  {name,-16} 빛총량=      0  픽셀=     0  평균색=-"));
                    continue;
                }

                string averageColor = $"({sumR / lit:0.00}, {sumG / lit:0.00}, {sumB / lit:0.00})";

                // 금색은 빨강·초록이 높고 파랑이 낮다.
                bool golden = sumB / lit < (sumR / lit) * 0.6f && sumG / lit > 0.3f;

                rows.Add((total,
                    $"  {name,-16} 빛총량={total,7:0}  픽셀={lit,6}  평균색={averageColor}{(golden ? "  <= 금색" : string.Empty)}"));
            }

            rows.Sort((a, b) => b.Total.CompareTo(a.Total));

            var report = new StringBuilder();
            report.AppendLine($"자식별 화면 기여 밝기 (t={string.Join(", ", TIMES)} 합산, 많은 순)");

            foreach ((float _, string line) in rows)
            {
                report.AppendLine(line);
            }

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
}
