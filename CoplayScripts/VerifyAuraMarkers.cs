using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 버프타워 이펙트 M2 검증 - 가공한 마커가 요구대로 나왔는지 잰다.
///
///   1) 루트 스케일 1 = 반경 1 (링 지름 2월드), 스케일 2 = 반경 2 (지름 4월드)
///   2) 링이 가로:세로 2:1로 눌려 있다
///   3) 영혼·강화의 화살표가 원점 위로 간다 (반전 확인)
///
/// 화살표 방향은 눈이 아니라 픽셀로 판정한다 - Marker 자식만 켜고 구워
/// 원점 행(row) 위/아래에 몇 픽셀이 있는지 센다.
/// </summary>
public static class VerifyAuraMarkers
{
    private const string AURA_ROOT = "Assets/Imported/Prefabs/Aura/";
    private const string REPORT_PATH = "output/vfx_check/aura_markers_verify.txt";
    private const string SHEET_PATH = "output/vfx_check/aura_markers_verify_sheet.png";

    private static readonly string[] TARGETS =
    {
        "FX_Aura_Time", "FX_Aura_Stealth", "FX_Aura_Soul", "FX_Aura_Enhancement",
    };

    private static readonly float[] RADII = { 1f, 2f };

    // 주기를 3초로 넓히고 수명을 12초로 늘렸으므로, 포화한 뒤(30초~) 한 주기를 훑는다.
    private static readonly float[] LOOP_SAMPLES =
    {
        30f, 30.375f, 30.75f, 31.125f, 31.5f, 31.875f, 32.25f, 32.625f, 33f,
    };

    private const string RING_CHILD = "Circle";
    private const string ARROW_CHILD = "Marker";

    private const int FRAME_SIZE = 256;
    private const float ORTHO_SIZE = 3f;
    private const float WIDE_ORTHO_SIZE = 8f;
    private const float CAMERA_DISTANCE = 10f;
    private const float ALPHA_THRESHOLD = 0.08f;
    private const float SAMPLE_TIME = 30f;
    private const int MEASURE_LAYER = 31;
    private const uint FIXED_SEED = 12345;

    // 드리프트 검사 - 반경 2 기준으로 이 범위를 벗어나면 흘러가는 것으로 본다.
    // 링은 반경 2(높이 ±1), 화살표는 위로 2~3월드까지가 정상이다.
    private const float DRIFT_ORTHO_SIZE = 12f;
    private const float DRIFT_RADIUS = 2f;
    private const float DRIFT_LIMIT_LOW = -2f;
    private const float DRIFT_LIMIT_HIGH = 5f;

    private static readonly float[] DRIFT_SAMPLES = { 5f, 15f, 30f, 45f, 60f };

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");

        var report = new StringBuilder();
        report.AppendLine("[VerifyAuraMarkers] 버프타워 이펙트 M2 검증");
        report.AppendLine($"  ortho={ORTHO_SIZE} frame={FRAME_SIZE} → 1월드 = " +
            $"{FRAME_SIZE / (ORTHO_SIZE * 2f):0.#}px");
        report.AppendLine();

        GameObject cameraHost = CreateCamera(out Camera camera);
        var target = new RenderTexture(FRAME_SIZE, FRAME_SIZE, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(FRAME_SIZE, FRAME_SIZE, TextureFormat.RGBA32, false);
        camera.targetTexture = target;

        var sheet = new Texture2D(
            FRAME_SIZE * RADII.Length, FRAME_SIZE * TARGETS.Length, TextureFormat.RGBA32, false);

        for (int t = 0; t < TARGETS.Length; t++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AURA_ROOT + TARGETS[t] + ".prefab");

            if (prefab == null)
            {
                report.AppendLine($"[없음] {TARGETS[t]}");
                continue;
            }

            report.AppendLine($"=== {TARGETS[t]}");

            for (int r = 0; r < RADII.Length; r++)
            {
                GameObject host = Spawn(prefab, RADII[r], null);
                Color[] full = Bake(host, camera, target, readback);
                Object.DestroyImmediate(host);

                sheet.SetPixels(
                    FRAME_SIZE * r, FRAME_SIZE * (TARGETS.Length - 1 - t),
                    FRAME_SIZE, FRAME_SIZE, full);

                // 링은 퍼져나가는 파동이라 한 시점만 재면 위상에 걸린다.
                // 가장 넓게 퍼진 순간을 반경으로 본다(BuildAuraMarkers의 배율 산출과 같은 기준).
                int width = 0;
                int height = 0;

                foreach (float sample in LOOP_SAMPLES)
                {
                    GameObject ringHost = Spawn(prefab, RADII[r], RING_CHILD);
                    Color[] ring = BakeAt(ringHost, camera, target, readback, sample);
                    Object.DestroyImmediate(ringHost);

                    Measure(ring, out int w, out int h, out _, out _);

                    // 폭과 높이를 각각 최대로 잡으면 서로 다른 위상의 값이 섞여
                    // 가로세로비가 왜곡된다. 가장 넓은 한 프레임의 값을 그대로 쓴다.
                    if (w > width)
                    {
                        width = w;
                        height = h;
                    }
                }

                float pixelsPerUnit = FRAME_SIZE / (ORTHO_SIZE * 2f);
                float worldWidth = width / pixelsPerUnit;
                float worldHeight = height / pixelsPerUnit;
                float ratio = height > 0 ? width / (float)height : 0f;

                report.AppendLine(
                    $"  반경 {RADII[r]}: 링 {worldWidth:0.##} x {worldHeight:0.##} 월드 " +
                    $"(목표 {RADII[r] * 2f:0.##} x {RADII[r]:0.##}) · 가로세로비 {ratio:0.##}");
            }

            GameObject arrowHost = Spawn(prefab, 1f, ARROW_CHILD);
            Color[] arrow = Bake(arrowHost, camera, target, readback);
            Object.DestroyImmediate(arrowHost);

            Measure(arrow, out _, out _, out int above, out int below);

            string verdict = above > below ? "위 ✓" : (below > above ? "아래" : "-");
            report.AppendLine($"  화살표: 원점 위 {above}px / 아래 {below}px → {verdict}");

            // 전체가 얼마나 높이 뻗는지 - 타워(약 1~2월드)를 넘기는지 보려는 것.
            // 좁은 카메라로는 프레임에 잘려 실제 높이를 못 잰다.
            camera.orthographicSize = WIDE_ORTHO_SIZE;

            GameObject wideHost = Spawn(prefab, 2f, null);
            Color[] wide = Bake(wideHost, camera, target, readback);
            Object.DestroyImmediate(wideHost);

            Measure(wide, out int wideWidth, out int wideHeight, out _, out _);

            float widePixelsPerUnit = FRAME_SIZE / (WIDE_ORTHO_SIZE * 2f);
            report.AppendLine(
                $"  전체(반경 2): {wideWidth / widePixelsPerUnit:0.##} x " +
                $"{wideHeight / widePixelsPerUnit:0.##} 월드");

            camera.orthographicSize = ORTHO_SIZE;

            // 오라는 무한 지속이라 상태이상 연출과 요구가 반대다 - 켜진 뒤로는 평탄해야 한다.
            // 루프 한 주기 안에서 픽셀이 얼마나 출렁이는지 잰다(설계 6절).
            int minLit = int.MaxValue;
            int maxLit = 0;

            foreach (float sample in LOOP_SAMPLES)
            {
                GameObject loopHost = Spawn(prefab, 2f, null);
                Color[] frame = BakeAt(loopHost, camera, target, readback, sample);
                Object.DestroyImmediate(loopHost);

                int lit = CountLit(frame);
                minLit = Mathf.Min(minLit, lit);
                maxLit = Mathf.Max(maxLit, lit);
            }

            float swing = maxLit > 0 ? (maxLit - minLit) * 100f / maxLit : 0f;
            report.AppendLine(
                $"  루프 변동: {minLit} ~ {maxLit}px · 변동폭 {swing:0}%");

            AppendDrift(prefab, camera, target, readback, report);

            report.AppendLine();
        }

        sheet.Apply();
        File.WriteAllBytes(SHEET_PATH, sheet.EncodeToPNG());

        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(cameraHost);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(readback);
        Object.DestroyImmediate(sheet);

        report.AppendLine($"시트: {SHEET_PATH} (가로 = 반경 1 / 2, 세로 = " +
            string.Join(" / ", TARGETS) + ")");

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    // 자식이 원점에서 얼마나 멀리 흘러가는지 본다.
    //
    // 수명을 늘릴 때 움직이는 자식까지 늘리면 입자가 계속 떠내려간다. 밝기·크기만 재면
    // 이 부류를 못 잡는다 - 실제로 fantasy의 Marker가 힘모듈 y=-2 때문에 땅으로 꺼졌는데
    // 변동폭·링 지름 지표는 전부 정상으로 나왔다.
    private static void AppendDrift(
        GameObject prefab, Camera camera, RenderTexture target, Texture2D readback,
        StringBuilder report)
    {
        camera.orthographicSize = DRIFT_ORTHO_SIZE;

        int childCount = prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
        float pixelsPerUnit = FRAME_SIZE / (DRIFT_ORTHO_SIZE * 2f);

        for (int c = 0; c < childCount; c++)
        {
            string childName = string.Empty;
            float lowest = float.MaxValue;
            float highest = float.MinValue;

            foreach (float time in DRIFT_SAMPLES)
            {
                GameObject host = Object.Instantiate(prefab);
                host.transform.position = Vector3.zero;
                host.transform.localScale = Vector3.one * DRIFT_RADIUS;

                var systems = host.GetComponentsInChildren<ParticleSystem>(true);
                childName = systems[c].name;

                for (int i = 0; i < systems.Length; i++)
                {
                    var renderer = systems[i].GetComponent<ParticleSystemRenderer>();

                    if (renderer != null)
                    {
                        renderer.enabled = i == c;
                    }

                    systems[i].useAutoRandomSeed = false;
                    systems[i].randomSeed = FIXED_SEED;
                }

                foreach (Transform child in host.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = MEASURE_LAYER;
                }

                Color[] pixels = BakeAt(host, camera, target, readback, time);
                Object.DestroyImmediate(host);

                Measure(pixels, out int width, out _, out _, out _);

                if (width == 0)
                {
                    continue;
                }

                FindVerticalExtent(pixels, out int minY, out int maxY);

                lowest = Mathf.Min(lowest, (minY - FRAME_SIZE / 2f) / pixelsPerUnit);
                highest = Mathf.Max(highest, (maxY - FRAME_SIZE / 2f) / pixelsPerUnit);
            }

            if (lowest > highest)
            {
                continue;
            }

            bool drifts = lowest < DRIFT_LIMIT_LOW || highest > DRIFT_LIMIT_HIGH;

            report.AppendLine(
                $"    드리프트 {childName,-32} y {lowest:0.##} ~ {highest:0.##}월드" +
                (drifts ? "  ⚠ 흘러감" : string.Empty));
        }

        camera.orthographicSize = ORTHO_SIZE;
    }

    private static void FindVerticalExtent(Color[] pixels, out int minY, out int maxY)
    {
        minY = FRAME_SIZE;
        maxY = -1;

        for (int p = 0; p < pixels.Length; p++)
        {
            if (pixels[p].a <= ALPHA_THRESHOLD)
            {
                continue;
            }

            int y = p / FRAME_SIZE;
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }
    }

    private static GameObject Spawn(GameObject prefab, float radius, string keepChild)
    {
        GameObject host = Object.Instantiate(prefab);
        host.transform.position = Vector3.zero;
        host.transform.localScale = Vector3.one * radius;

        foreach (ParticleSystem particles in host.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();

            if (renderer != null && keepChild != null)
            {
                renderer.enabled = particles.name == keepChild;
            }

            // 시드를 고정하지 않으면 인스턴스마다 입자 위상이 달라져, 시간에 따른 변동과
            // 시드 차이가 섞인다. 루프 변동폭을 재려면 시드가 같아야 한다.
            particles.useAutoRandomSeed = false;
            particles.randomSeed = FIXED_SEED;
        }

        foreach (Transform child in host.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = MEASURE_LAYER;
        }

        return host;
    }

    private static Color[] Bake(
        GameObject host, Camera camera, RenderTexture target, Texture2D readback)
    {
        return BakeAt(host, camera, target, readback, SAMPLE_TIME);
    }

    private static int CountLit(Color[] pixels)
    {
        int lit = 0;

        for (int p = 0; p < pixels.Length; p++)
        {
            if (pixels[p].a > ALPHA_THRESHOLD)
            {
                lit++;
            }
        }

        return lit;
    }

    private static Color[] BakeAt(
        GameObject host, Camera camera, RenderTexture target, Texture2D readback, float time)
    {
        var systems = host.GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem particles in systems)
        {
            particles.Clear(true);
        }

        foreach (ParticleSystem particles in systems)
        {
            particles.Simulate(time, false, true);
        }

        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, FRAME_SIZE, FRAME_SIZE), 0, 0);
        readback.Apply();
        RenderTexture.active = previous;

        return readback.GetPixels();
    }

    // 원점은 프레임 중앙 행이다(카메라가 원점을 본다).
    private static void Measure(
        Color[] pixels, out int width, out int height, out int above, out int below)
    {
        int minX = FRAME_SIZE;
        int maxX = -1;
        int minY = FRAME_SIZE;
        int maxY = -1;
        above = 0;
        below = 0;

        int centerRow = FRAME_SIZE / 2;

        for (int p = 0; p < pixels.Length; p++)
        {
            if (pixels[p].a <= ALPHA_THRESHOLD)
            {
                continue;
            }

            int x = p % FRAME_SIZE;
            int y = p / FRAME_SIZE;

            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);

            if (y > centerRow)
            {
                above++;
            }
            else if (y < centerRow)
            {
                below++;
            }
        }

        width = maxX >= 0 ? maxX - minX + 1 : 0;
        height = maxY >= 0 ? maxY - minY + 1 : 0;
    }

    private static GameObject CreateCamera(out Camera camera)
    {
        var host = new GameObject("AuraMarkerVerifyCamera");
        camera = host.AddComponent<Camera>();

        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, 0f, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.cullingMask = 1 << MEASURE_LAYER;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;

        return host;
    }
}
