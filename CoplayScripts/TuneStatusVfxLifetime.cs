using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태 지속 연출의 입자 수명 배수 후보를 실제로 구워 비교한다.
///
/// 지금 문제: 화상 3초인데 연출이 포화까지 4초 걸린다. 걸린 직후 1초가 22~38%라
/// "무엇이 걸렸는지" 알려야 할 구간이 가장 흐리다.
///
/// 원인이 두 겹이라 수명 하나로 둘 다 잡힌다.
/// - 누적: 연속 방출이라 입자가 쌓이는 데 시간이 걸린다
/// - 알파 곡선: (0,0)(0.29,0.93)(1,0) - 각 입자가 수명의 29%가 지나야 최대 밝기다.
///   수명 5초면 1.45초, 1.5초면 0.44초.
///
/// 목표: 0.3초에 이미 알아볼 수 있고, 3초 동안 크게 변하지 않는 것.
/// 판정 기준은 개수가 아니라 픽셀이다(작업노트 3-1).
/// </summary>
public static class TuneStatusVfxLifetime
{
    private const string VFX_PATH =
        "Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn.prefab";
    private const string OUTPUT_PATH = "output/vfx_check/status_vfx_lifetime_tune.txt";
    private const string SHEET_PATH = "output/vfx_check/status_vfx_lifetime_tune.png";

    private const int FRAME_SIZE = 256;
    private const float ORTHO_SIZE = 1.2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float ALPHA_THRESHOLD = 0.08f;
    private const int MEASURE_LAYER = 31;

    // 수명만으로는 초반 밝기가 안 오른다 - 알파 곡선이 (0,0)에서 시작하므로 갓 태어난 입자는
    // 투명하고, 초반 밝기를 정하는 것은 "그때까지 쌓인 입자 수" 즉 방출률이다.
    // 그래서 두 축을 함께 본다: (수명 배수, 방출 배수).
    private static readonly (float Lifetime, float Rate)[] CANDIDATES =
    {
        (1f, 1f),       // 현재값 - 비교 기준
        (0.35f, 1f),    // 수명만 줄임
        (0.35f, 3f),    // 수명 줄이고 방출 3배
        (0.25f, 4f),    // 더 짧고 더 빽빽하게
    };

    private static readonly float[] SAMPLE_TIMES = { 0.15f, 0.3f, 0.6f, 1f, 2f, 3f };

    public static void Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_PATH);

        if (prefab == null)
        {
            Debug.LogError($"[TuneStatusVfxLifetime] 프리팹이 없습니다: {VFX_PATH}");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("[TuneStatusVfxLifetime] 입자 수명 배수 비교");
        report.AppendLine("  목표: 0.3초에 알아볼 수 있고 3초 동안 안정적일 것");
        report.AppendLine();

        GameObject cameraHost = CreateCamera(out Camera camera);
        var target = new RenderTexture(FRAME_SIZE, FRAME_SIZE, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(FRAME_SIZE, FRAME_SIZE, TextureFormat.RGBA32, false);
        camera.targetTexture = target;

        var sheet = new Texture2D(
            FRAME_SIZE * SAMPLE_TIMES.Length,
            FRAME_SIZE * CANDIDATES.Length,
            TextureFormat.RGBA32,
            false);

        // 모든 후보를 한 기준으로 비교하려면 최대 픽셀이 공통이어야 한다(작업노트 3-6).
        var allCounts = new List<int[]>();
        int globalMax = 1;

        for (int s = 0; s < CANDIDATES.Length; s++)
        {
            (float scale, float rateScale) = CANDIDATES[s];
            GameObject instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;

            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = MEASURE_LAYER;
            }

            var systems = instance.GetComponentsInChildren<ParticleSystem>(true);

            foreach (ParticleSystem particles in systems)
            {
                ScaleLifetime(particles, scale);
                ScaleRate(particles, rateScale);
            }

            ParticleSystem root = instance.GetComponent<ParticleSystem>();
            var counts = new int[SAMPLE_TIMES.Length];

            for (int i = 0; i < SAMPLE_TIMES.Length; i++)
            {
                foreach (ParticleSystem particles in systems)
                {
                    particles.Clear(true);
                }

                if (root != null)
                {
                    root.Simulate(SAMPLE_TIMES[i], true, true);
                }
                else
                {
                    foreach (ParticleSystem particles in systems)
                    {
                        particles.Simulate(SAMPLE_TIMES[i], true, true);
                    }
                }

                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, FRAME_SIZE, FRAME_SIZE), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;

                Color[] pixels = readback.GetPixels();
                int lit = 0;

                foreach (Color pixel in pixels)
                {
                    if (pixel.a > ALPHA_THRESHOLD)
                    {
                        lit++;
                    }
                }

                counts[i] = lit;
                globalMax = Mathf.Max(globalMax, lit);

                // 위에서부터 채운다 - 첫 줄이 현재값이라야 읽기 쉽다.
                int row = (CANDIDATES.Length - 1 - s) * FRAME_SIZE;
                sheet.SetPixels(FRAME_SIZE * i, row, FRAME_SIZE, FRAME_SIZE, pixels);
            }

            allCounts.Add(counts);
            Object.DestroyImmediate(instance);
        }

        report.Append("  수명×방출 | 실제 수명 ");

        foreach (float t in SAMPLE_TIMES)
        {
            report.Append($"| {t:0.##}초 ");
        }

        report.AppendLine("| 판정");

        for (int s = 0; s < CANDIDATES.Length; s++)
        {
            (float scale, float rateScale) = CANDIDATES[s];
            int[] counts = allCounts[s];

            report.Append(
                $"  ×{scale:0.##}/×{rateScale:0.##} | {5f * scale:0.##}~{6f * scale:0.##}초 ");

            foreach (int count in counts)
            {
                report.Append($"| {count * 100f / globalMax:0}% ");
            }

            // 0.3초 밝기와 3초까지의 변동폭으로 읽는다.
            float early = counts[1] * 100f / globalMax;
            float late = counts[SAMPLE_TIMES.Length - 1] * 100f / globalMax;
            float swing = Mathf.Abs(late - early);

            report.AppendLine($"| 초반 {early:0}%, 변동 {swing:0}%p");
        }

        sheet.Apply();
        System.IO.File.WriteAllBytes(SHEET_PATH, sheet.EncodeToPNG());

        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(cameraHost);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(readback);
        Object.DestroyImmediate(sheet);

        report.AppendLine();
        report.AppendLine($"시트: {SHEET_PATH}");
        report.AppendLine($"  가로: {string.Join(" / ", SAMPLE_TIMES)}초");
        var rows = new List<string>();

        foreach ((float lifetime, float rate) in CANDIDATES)
        {
            rows.Add($"수명×{lifetime:0.##} 방출×{rate:0.##}");
        }

        report.AppendLine($"  세로(위→아래): {string.Join(" / ", rows)}");

        System.IO.File.WriteAllText(OUTPUT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static void ScaleLifetime(ParticleSystem particles, float scale)
    {
        ParticleSystem.MainModule main = particles.main;
        ParticleSystem.MinMaxCurve lifetime = main.startLifetime;

        switch (lifetime.mode)
        {
            case ParticleSystemCurveMode.TwoConstants:
                lifetime.constantMin *= scale;
                lifetime.constantMax *= scale;
                break;

            default:
                lifetime.constant *= scale;
                break;
        }

        main.startLifetime = lifetime;
    }

    private static void ScaleRate(ParticleSystem particles, float scale)
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;

        // constant는 constantMax의 별칭이다 - 둘 다 곱하면 배수가 제곱으로 걸린다.
        if (rate.mode == ParticleSystemCurveMode.TwoConstants)
        {
            rate.constantMin *= scale;
            rate.constantMax *= scale;
        }
        else
        {
            rate.constant *= scale;
        }

        emission.rateOverTime = rate;
    }

    private static GameObject CreateCamera(out Camera camera)
    {
        var host = new GameObject("StatusVfxTuneCamera (temp)");
        camera = host.AddComponent<Camera>();

        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.transform.position = new Vector3(0f, ORTHO_SIZE * 0.6f, -CAMERA_DISTANCE);
        camera.cullingMask = 1 << MEASURE_LAYER;

        return host;
    }
}
