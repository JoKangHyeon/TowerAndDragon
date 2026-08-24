using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태 지속 연출이 시간대별로 실제 몇 픽셀을 그리는지 잰다.
///
/// 입자 개수로는 "보이는가"를 알 수 없다 - 작업노트 3-1·3-2가 이름과 개수로 판단했다가
/// 두 번 틀린 기록이다. 여기서는 실제로 구워서 임계값을 넘는 픽셀을 센다.
///
/// 카메라 배율은 전 프레임 고정이다(3-6: orthographicSize가 다르면 잰 값이 통째로 어긋난다).
/// sizeOverLifetime·colorOverLifetime도 함께 찍는다 - 누적 지연 위에 곡선 지연이 겹치면
/// 수명만 줄여서는 해결되지 않기 때문이다.
/// </summary>
public static class MeasureStatusVfxPixels
{
    private const string VFX_PATH =
        "Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn.prefab";
    private const string OUTPUT_PATH = "output/vfx_check/status_vfx_pixels.txt";
    private const string SHEET_PATH = "output/vfx_check/status_vfx_ramp_sheet.png";

    private const int FRAME_SIZE = 256;
    // 루트 스케일 0.18에 startSize 8짜리 자식이 있어 좁게 잡으면 프레임을 넘겨 포화로 읽힌다.
    private const float ORTHO_SIZE = 1.2f;
    private const float CAMERA_DISTANCE = 10f;
    private const float ALPHA_THRESHOLD = 0.08f;
    private const float BURN_DURATION = 3f;

    // 씬의 다른 것이 프레임에 섞이지 않도록 연출만 올려 두는 레이어.
    // 31은 유니티가 예약하지 않은 마지막 레이어라 프로젝트 설정과 부딪히지 않는다.
    private const int MEASURE_LAYER = 31;

    private static readonly float[] SAMPLE_TIMES =
    {
        0.15f, 0.25f, 0.5f, 0.75f, 1f, 1.5f, 2f, 2.5f, 3f, 4f, 5f, 6f,
    };

    public static void Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_PATH);

        if (prefab == null)
        {
            Debug.LogError($"[MeasureStatusVfxPixels] 프리팹이 없습니다: {VFX_PATH}");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"[MeasureStatusVfxPixels] {prefab.name}");
        report.AppendLine($"  ortho={ORTHO_SIZE} frame={FRAME_SIZE} 임계 알파={ALPHA_THRESHOLD}");
        report.AppendLine();

        AppendCurveInfo(prefab, report);

        GameObject instance = Object.Instantiate(prefab);
        instance.transform.position = Vector3.zero;

        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = MEASURE_LAYER;
        }

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        ParticleSystem root = instance.GetComponent<ParticleSystem>();

        GameObject cameraHost = CreateCamera(out Camera camera);
        var target = new RenderTexture(FRAME_SIZE, FRAME_SIZE, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(FRAME_SIZE, FRAME_SIZE, TextureFormat.RGBA32, false);

        camera.targetTexture = target;

        var sheet = new Texture2D(
            FRAME_SIZE * SAMPLE_TIMES.Length, FRAME_SIZE, TextureFormat.RGBA32, false);

        report.AppendLine("시간별 그려진 픽셀:");
        report.AppendLine("  t(초) | 픽셀 | 최대 대비 | 높이(위로 솟은 정도)");

        var pixelCounts = new int[SAMPLE_TIMES.Length];
        var topRows = new int[SAMPLE_TIMES.Length];

        for (int i = 0; i < SAMPLE_TIMES.Length; i++)
        {
            float t = SAMPLE_TIMES[i];

            foreach (ParticleSystem particles in systems)
            {
                particles.Clear(true);
            }

            if (root != null)
            {
                root.Simulate(t, true, true);
            }
            else
            {
                foreach (ParticleSystem particles in systems)
                {
                    particles.Simulate(t, true, true);
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
            int topRow = -1;

            for (int p = 0; p < pixels.Length; p++)
            {
                if (pixels[p].a <= ALPHA_THRESHOLD)
                {
                    continue;
                }

                lit++;

                int row = p / FRAME_SIZE;

                if (row > topRow)
                {
                    topRow = row;
                }
            }

            pixelCounts[i] = lit;
            topRows[i] = topRow;

            sheet.SetPixels(FRAME_SIZE * i, 0, FRAME_SIZE, FRAME_SIZE, pixels);
        }

        int maxPixels = 1;

        foreach (int count in pixelCounts)
        {
            maxPixels = Mathf.Max(maxPixels, count);
        }

        for (int i = 0; i < SAMPLE_TIMES.Length; i++)
        {
            float ratio = pixelCounts[i] * 100f / maxPixels;
            string marker = Mathf.Abs(SAMPLE_TIMES[i] - BURN_DURATION) < 0.001f
                ? "   ← 화상 종료"
                : string.Empty;

            report.AppendLine(
                $"  {SAMPLE_TIMES[i]:0.00} | {pixelCounts[i]} | {ratio:0}% | {topRows[i]}{marker}");
        }

        sheet.Apply();
        System.IO.File.WriteAllBytes(SHEET_PATH, sheet.EncodeToPNG());

        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(cameraHost);
        Object.DestroyImmediate(instance);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(readback);
        Object.DestroyImmediate(sheet);

        report.AppendLine();
        report.AppendLine($"시트: {SHEET_PATH} (왼쪽부터 {string.Join(" / ", SAMPLE_TIMES)}초)");

        System.IO.File.WriteAllText(OUTPUT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    // 누적 지연 위에 곡선 지연이 겹치는지 확인한다.
    private static void AppendCurveInfo(GameObject prefab, StringBuilder report)
    {
        report.AppendLine("크기·색 곡선 (누적 지연에 더해지는 두 번째 지연):");

        foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;

            string sizeInfo = size.enabled ? DescribeSizeCurve(size) : "꺼짐";
            string colorInfo = color.enabled ? DescribeColorCurve(color) : "꺼짐";

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            string material = renderer != null && renderer.sharedMaterial != null
                ? renderer.sharedMaterial.name
                : "<없음>";

            report.AppendLine(
                $"  {particles.name}: size={sizeInfo} color={colorInfo} mat={material}");
        }

        report.AppendLine();
    }

    private static string DescribeSizeCurve(ParticleSystem.SizeOverLifetimeModule size)
    {
        ParticleSystem.MinMaxCurve curve = size.size;

        if (curve.curve == null || curve.curve.length == 0)
        {
            return $"상수 {curve.constant:0.##}";
        }

        var parts = new StringBuilder();

        foreach (Keyframe key in curve.curve.keys)
        {
            parts.Append($"({key.time:0.##},{key.value:0.##})");
        }

        return parts.ToString();
    }

    private static string DescribeColorCurve(ParticleSystem.ColorOverLifetimeModule color)
    {
        Gradient gradient = color.color.gradient;

        if (gradient == null)
        {
            return "그라디언트 없음";
        }

        var parts = new StringBuilder("알파");

        foreach (GradientAlphaKey key in gradient.alphaKeys)
        {
            parts.Append($"({key.time:0.##},{key.alpha:0.##})");
        }

        return parts.ToString();
    }

    private static GameObject CreateCamera(out Camera camera)
    {
        var host = new GameObject("StatusVfxPixelCamera (temp)");
        camera = host.AddComponent<Camera>();

        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.transform.position = new Vector3(0f, ORTHO_SIZE * 0.6f, -CAMERA_DISTANCE);

        // 씬의 타일·성이 프레임에 들어오면 그 픽셀까지 세어 측정이 통째로 무효가 된다.
        // 연출만 전용 레이어로 옮기고 카메라도 그 레이어만 보게 한다.
        camera.cullingMask = 1 << MEASURE_LAYER;

        return host;
    }
}
