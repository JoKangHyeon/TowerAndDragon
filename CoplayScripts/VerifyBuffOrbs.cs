using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 버프타워 이펙트 M3 검증.
///
///   1) 구체 하나가 목표 규격인가 - 지름 0.4, 비율 1.0, 루프 변동 0%, 드리프트 없음
///   2) 네 색이 실제로 서로 다른가 - 평균 RGB를 재서 비교한다
///   3) 호선 배치가 대칭인가 - 1~4개를 실제 타워 위에 얹어 굽고 무게중심을 잰다
///
/// 3번이 이 검증의 본체다. 설계 3-1의 "몇 개가 되든 무리의 중심이 타워 중심축 위에" 를
/// 눈이 아니라 픽셀 무게중심으로 판정한다.
/// </summary>
public static class VerifyBuffOrbs
{
    private const string AURA_ROOT = "Assets/Imported/Prefabs/Aura/";
    private const string TOWER_PREFAB = "Assets/Data/TowerData/TowerPrefab/TP_Arrow.prefab";
    private const string REPORT_PATH = "output/vfx_check/buff_orbs_verify.txt";
    private const string SHEET_PATH = "output/vfx_check/buff_orbs_verify_sheet.png";

    // 좌우 순서 고정 (설계 3-3): 시간 → 강화 → 영혼 → 은신
    private static readonly string[] ORBS =
    {
        "FX_BuffOrb_Time", "FX_BuffOrb_Enhancement", "FX_BuffOrb_Soul", "FX_BuffOrb_Stealth",
    };

    // 설계 3-2의 시작값
    private const float SPACING = 0.36f;
    private const float BASE_HEIGHT = 1.9f;
    private const float CURVATURE = 0.5f;

    private const int FRAME_SIZE = 256;
    private const float ORTHO_SIZE = 2f;
    private const float CAMERA_DISTANCE = 10f;
    // 불투명 검정 배경이므로 알파가 아니라 밝기(RGB 최대)로 판정한다.
    private const float LUMA_THRESHOLD = 0.04f;
    private const int MEASURE_LAYER = 31;
    private const uint FIXED_SEED = 12345;

    private static readonly float[] SAMPLES = { 2f, 2.3f, 2.6f, 2.9f, 3.2f, 3.5f };

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");

        var report = new StringBuilder();
        report.AppendLine("[VerifyBuffOrbs] 버프타워 이펙트 M3 검증");
        report.AppendLine($"  ortho={ORTHO_SIZE} → 1월드 = {FRAME_SIZE / (ORTHO_SIZE * 2f):0.#}px");
        report.AppendLine();

        GameObject cameraHost = CreateCamera(out Camera camera);
        var target = new RenderTexture(FRAME_SIZE, FRAME_SIZE, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(FRAME_SIZE, FRAME_SIZE, TextureFormat.RGBA32, false);
        camera.targetTexture = target;

        MeasureOrbs(camera, target, readback, report);
        MeasureArc(camera, target, readback, report);

        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(cameraHost);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(readback);

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static void MeasureOrbs(
        Camera camera, RenderTexture target, Texture2D readback, StringBuilder report)
    {
        report.AppendLine("구체 규격 (목표: 지름 0.4 · 비율 1.0 · 변동 0% · 드리프트 없음):");
        report.AppendLine($"  {"프리팹",-24}{"크기(월드)",-16}{"비율",6}{"변동",7}{"드리프트",14}{"평균 RGB"}");

        float pixelsPerUnit = FRAME_SIZE / (ORTHO_SIZE * 2f);

        foreach (string orbName in ORBS)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AURA_ROOT + orbName + ".prefab");

            if (prefab == null)
            {
                report.AppendLine($"  [없음] {orbName}");
                continue;
            }

            int minLit = int.MaxValue;
            int maxLit = 0;
            float maxWidth = 0f;
            float maxHeight = 0f;
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            var sum = Vector3.zero;
            int litTotal = 0;
            float peakAlpha = 0f;
            int strongPixels = 0;

            foreach (float time in SAMPLES)
            {
                GameObject host = Spawn(prefab, Vector3.zero);
                Color[] pixels = Bake(host, camera, target, readback, time);
                Object.DestroyImmediate(host);

                // 눈에 보이는가를 판정하려면 넓이가 아니라 밝기를 봐야 한다.
                // 임계값을 낮춰서 "그려진다"고 읽은 것이 실제로는 거의 투명일 수 있다.
                foreach (Color pixel in pixels)
                {
                    float luma = Luma(pixel);
                    peakAlpha = Mathf.Max(peakAlpha, luma);

                    if (luma > 0.5f)
                    {
                        strongPixels++;
                    }
                }

                int lit = Measure(
                    pixels, out int w, out int h, out int minY, out int maxY, out Vector3 rgb);

                minLit = Mathf.Min(minLit, lit);
                maxLit = Mathf.Max(maxLit, lit);

                if (lit == 0)
                {
                    continue;
                }

                maxWidth = Mathf.Max(maxWidth, w / pixelsPerUnit);
                maxHeight = Mathf.Max(maxHeight, h / pixelsPerUnit);
                lowest = Mathf.Min(lowest, (minY - FRAME_SIZE / 2f) / pixelsPerUnit);
                highest = Mathf.Max(highest, (maxY - FRAME_SIZE / 2f) / pixelsPerUnit);
                sum += rgb * lit;
                litTotal += lit;
            }

            if (maxLit == 0)
            {
                report.AppendLine($"  {orbName,-24}(안 그려짐)");
                continue;
            }

            float swing = (maxLit - minLit) * 100f / maxLit;
            float ratio = maxHeight > 0f ? maxWidth / maxHeight : 0f;
            Vector3 average = sum / Mathf.Max(1, litTotal);

            report.AppendLine(
                $"  {orbName,-24}{maxWidth:0.##} x {maxHeight:0.##}".PadRight(42) +
                $"{ratio,6:0.##}{swing,6:0}%{lowest,7:0.##}~{highest:0.##}   " +
                $"({average.x:0.00}, {average.y:0.00}, {average.z:0.00})" +
                $"  최대밝기 {peakAlpha:0.00}  밝기>0.5 {strongPixels}px");
        }

        report.AppendLine();
    }

    // 설계 3-1의 대칭 규칙을 픽셀 무게중심으로 판정한다.
    private static void MeasureArc(
        Camera camera, RenderTexture target, Texture2D readback, StringBuilder report)
    {
        var towerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TOWER_PREFAB);

        report.AppendLine("호선 배치 대칭 (설계 3-1: 무리의 중심이 타워 중심축 위에):");
        report.AppendLine($"  SPACING={SPACING} BASE_HEIGHT={BASE_HEIGHT} CURVATURE={CURVATURE}");
        report.AppendLine($"  {"개수",6}{"무게중심 x",12}{"전체 폭",10}{"y 범위",16}");

        float pixelsPerUnit = FRAME_SIZE / (ORTHO_SIZE * 2f);

        var sheet = new Texture2D(FRAME_SIZE * 4, FRAME_SIZE, TextureFormat.RGBA32, false);

        for (int n = 1; n <= ORBS.Length; n++)
        {
            var hosts = new GameObject[n];

            for (int i = 0; i < n; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AURA_ROOT + ORBS[i] + ".prefab");

                if (prefab == null)
                {
                    continue;
                }

                float x = (i - (n - 1) / 2f) * SPACING;
                float y = BASE_HEIGHT - CURVATURE * x * x;

                hosts[i] = Spawn(prefab, new Vector3(x, y, 0f));
            }

            // 타워는 참고용으로만 띄운다 - 무게중심 판정에는 넣지 않는다.
            GameObject tower = null;

            if (towerPrefab != null)
            {
                tower = Object.Instantiate(towerPrefab);
                tower.transform.position = Vector3.zero;

                foreach (Transform child in tower.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = MEASURE_LAYER;
                }
            }

            // 카메라를 타워와 구체가 함께 들어오는 높이로 옮긴다.
            camera.transform.position = new Vector3(0f, 1.2f, -CAMERA_DISTANCE);

            foreach (GameObject host in hosts)
            {
                if (host != null)
                {
                    SimulateOnly(host, SAMPLES[0]);
                }
            }

            camera.Render();
            Color[] withTower = ReadFrame(camera, target, readback);

            if (tower != null)
            {
                Object.DestroyImmediate(tower);
            }

            // 구체만 다시 굽는다 - 타워 픽셀이 섞이면 무게중심이 오염된다.
            camera.Render();
            Color[] orbsOnly = ReadFrame(camera, target, readback);

            foreach (GameObject host in hosts)
            {
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                }
            }

            sheet.SetPixels(FRAME_SIZE * (n - 1), 0, FRAME_SIZE, FRAME_SIZE, withTower);

            // 구체가 "눈에 띄는가"는 절대 밝기가 아니라 **타워 대비**로 정해진다.
            // 같은 프레임에서 타워 몸통 밝기와 구체 밝기를 비교한다.
            float towerLuma = PeakLumaInBand(withTower, 0, FRAME_SIZE / 2);
            float orbLuma = PeakLumaInBand(orbsOnly, FRAME_SIZE / 2, FRAME_SIZE);

            MeasureCentroid(
                orbsOnly, out float centroidX, out int width, out int minY, out int maxY);

            float centroidWorld = (centroidX - FRAME_SIZE / 2f) / pixelsPerUnit;
            float lowWorld = (minY - FRAME_SIZE / 2f) / pixelsPerUnit + 1.2f;
            float highWorld = (maxY - FRAME_SIZE / 2f) / pixelsPerUnit + 1.2f;

            float contrast = towerLuma > 0f ? orbLuma / towerLuma : 0f;

            report.AppendLine(
                $"  {n,6}{centroidWorld,12:0.###}{width / pixelsPerUnit,10:0.##}" +
                $"{lowWorld,8:0.##}~{highWorld:0.##}" +
                $"   구체 {orbLuma:0.00} / 타워 {towerLuma:0.00} = {contrast:0.00}배");
        }

        sheet.Apply();
        File.WriteAllBytes(SHEET_PATH, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);

        camera.transform.position = new Vector3(0f, 0f, -CAMERA_DISTANCE);

        report.AppendLine();
        report.AppendLine("  무게중심 x가 0에 가까울수록 대칭이다 (설계 3-1의 절대 규칙)");
        report.AppendLine($"시트: {SHEET_PATH} (가로 = 1 / 2 / 3 / 4개, 타워 포함)");
    }

    private static GameObject Spawn(GameObject prefab, Vector3 position)
    {
        GameObject host = Object.Instantiate(prefab);
        host.transform.position = position;

        foreach (ParticleSystem particles in host.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.useAutoRandomSeed = false;
            particles.randomSeed = FIXED_SEED;
        }

        foreach (Transform child in host.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = MEASURE_LAYER;
        }

        return host;
    }

    private static void SimulateOnly(GameObject host, float time)
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
    }

    private static Color[] Bake(
        GameObject host, Camera camera, RenderTexture target, Texture2D readback, float time)
    {
        SimulateOnly(host, time);
        camera.Render();

        return ReadFrame(camera, target, readback);
    }

    private static Color[] ReadFrame(Camera camera, RenderTexture target, Texture2D readback)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, FRAME_SIZE, FRAME_SIZE), 0, 0);
        readback.Apply();
        RenderTexture.active = previous;

        return readback.GetPixels();
    }

    private static int Measure(
        Color[] pixels, out int width, out int height,
        out int minY, out int maxY, out Vector3 averageRgb)
    {
        int lit = 0;
        int minX = FRAME_SIZE;
        int maxX = -1;
        minY = FRAME_SIZE;
        maxY = -1;
        var sum = Vector3.zero;

        for (int p = 0; p < pixels.Length; p++)
        {
            if (Luma(pixels[p]) <= LUMA_THRESHOLD)
            {
                continue;
            }

            lit++;

            int x = p % FRAME_SIZE;
            int y = p / FRAME_SIZE;

            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);

            sum += new Vector3(pixels[p].r, pixels[p].g, pixels[p].b);
        }

        width = maxX >= 0 ? maxX - minX + 1 : 0;
        height = maxY >= 0 ? maxY - minY + 1 : 0;
        averageRgb = lit > 0 ? sum / lit : Vector3.zero;

        return lit;
    }

    // 알파 가중 무게중심 - 옅은 가장자리까지 반영해야 대칭 판정이 정확하다.
    private static void MeasureCentroid(
        Color[] pixels, out float centroidX, out int width, out int minY, out int maxY)
    {
        float weighted = 0f;
        float total = 0f;
        int minX = FRAME_SIZE;
        int maxX = -1;
        minY = FRAME_SIZE;
        maxY = -1;

        for (int p = 0; p < pixels.Length; p++)
        {
            float alpha = Luma(pixels[p]);

            if (alpha <= LUMA_THRESHOLD)
            {
                continue;
            }

            int x = p % FRAME_SIZE;
            int y = p / FRAME_SIZE;

            weighted += x * alpha;
            total += alpha;

            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }

        centroidX = total > 0f ? weighted / total : FRAME_SIZE / 2f;
        width = maxX >= 0 ? maxX - minX + 1 : 0;
    }

    // 프레임의 특정 가로 띠에서 가장 밝은 값. 타워(아래쪽)와 구체(위쪽)를 나눠 재려는 것이다.
    private static float PeakLumaInBand(Color[] pixels, int fromRow, int toRow)
    {
        float peak = 0f;

        for (int y = fromRow; y < toRow; y++)
        {
            for (int x = 0; x < FRAME_SIZE; x++)
            {
                peak = Mathf.Max(peak, Luma(pixels[y * FRAME_SIZE + x]));
            }
        }

        return peak;
    }

    // 가산 블렌드 연출은 검정 배경 위 밝기가 곧 화면에서 보이는 정도다.
    private static float Luma(Color color) =>
        Mathf.Max(color.r, Mathf.Max(color.g, color.b));

    private static GameObject CreateCamera(out Camera camera)
    {
        var host = new GameObject("BuffOrbVerifyCamera");
        camera = host.AddComponent<Camera>();

        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, 0f, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        // 불투명 검정 배경에 굽는다.
        // 이 셰이더는 가산 계열이라 투명 배경에 구우면 RGB는 밝은데 알파가 0.03으로 읽힌다 -
        // 알파로 "보이는가"를 판정하면 멀쩡한 연출을 안 보인다고 오독한다.
        // 검정 위에 얹으면 화면에서 보이는 밝기가 그대로 RGB로 나온다.
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 1f);
        camera.cullingMask = 1 << MEASURE_LAYER;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;

        return host;
    }
}
