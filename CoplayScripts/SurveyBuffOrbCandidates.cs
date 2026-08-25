using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 수혜자 표식(B 슬롯)을 "작은 구체"로 만들 자식을 고른다.
///
/// VFX_Buff_Loop / VFX_Heal_Loop은 통째로 쓰기엔 크고 요소가 많다(자식 9~10개).
/// 구체로 쓸 만한 자식은 (1) 둥글고 (2) 제자리에 있고 (3) 루프에서 평탄한 것이다.
///
/// 자식 하나만 켜고 구워 폭·높이·가로세로비를 재고, 시간축으로 훑어
/// 변동폭과 드리프트를 함께 낸다. 이름으로 고르지 않는다(작업노트 3-1).
/// </summary>
public static class SurveyBuffOrbCandidates
{
    private const string VFX_ROOT = "Assets/Imported/Vefects/Anime VFX URP/Shared/Particles/";
    private const string REPORT_PATH = "output/vfx_check/buff_orb_candidates.txt";
    private const string SHEET_PATH = "output/vfx_check/buff_orb_candidates_sheet.png";

    private static readonly string[] TARGETS = { "VFX_Buff_Loop", "VFX_Heal_Loop" };

    private const int FRAME_SIZE = 160;

    // 카메라를 넓게 잡는다 - 좁으면 멀리 흘러간 입자가 프레임에 잘려 드리프트가 작게 읽힌다.
    private const float ORTHO_SIZE = 4f;
    private const float CAMERA_DISTANCE = 10f;

    // 0.08은 옅은 Glow를 통째로 놓친다(작업노트 3-1의 floorglow와 같은 false negative).
    // 구체 후보는 원래 은은한 것이 정상이므로 임계값을 낮춰 다시 본다.
    private const float ALPHA_THRESHOLD = 0.015f;
    private const int MEASURE_LAYER = 31;
    private const uint FIXED_SEED = 12345;

    private static readonly float[] SAMPLES = { 2f, 2.25f, 2.5f, 2.75f, 3f, 3.25f, 3.5f, 3.75f };

    private const int SHEET_COLUMNS = 8;

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");

        var report = new StringBuilder();
        report.AppendLine("[SurveyBuffOrbCandidates] 구체로 쓸 자식 고르기");
        report.AppendLine($"  ortho={ORTHO_SIZE} → 1월드 = {FRAME_SIZE / (ORTHO_SIZE * 2f):0.#}px");
        report.AppendLine("  루프 변동이 작고 · 드리프트가 없고 · 가로세로비가 1에 가까운 자식이 후보다");
        report.AppendLine();

        GameObject cameraHost = CreateCamera(out Camera camera);
        var target = new RenderTexture(FRAME_SIZE, FRAME_SIZE, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(FRAME_SIZE, FRAME_SIZE, TextureFormat.RGBA32, false);
        camera.targetTexture = target;

        int totalRows = 0;

        foreach (string targetName in TARGETS)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_ROOT + targetName + ".prefab");

            if (prefab != null)
            {
                totalRows += prefab.GetComponentsInChildren<ParticleSystem>(true).Length;
            }
        }

        var sheet = new Texture2D(
            FRAME_SIZE * SHEET_COLUMNS, FRAME_SIZE * Mathf.Max(1, totalRows),
            TextureFormat.RGBA32, false);

        int row = 0;

        foreach (string targetName in TARGETS)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_ROOT + targetName + ".prefab");

            if (prefab == null)
            {
                report.AppendLine($"[없음] {targetName}");
                continue;
            }

            report.AppendLine($"=== {targetName}");
            report.AppendLine($"  {"자식",-14}{"크기(월드)",-16}{"비율",6}{"변동",7}{"드리프트",12}");

            int childCount = prefab.GetComponentsInChildren<ParticleSystem>(true).Length;

            for (int c = 0; c < childCount; c++)
            {
                string childName = string.Empty;
                int minLit = int.MaxValue;
                int maxLit = 0;
                float maxWidth = 0f;
                float maxHeight = 0f;
                float lowest = float.MaxValue;
                float highest = float.MinValue;

                float pixelsPerUnit = FRAME_SIZE / (ORTHO_SIZE * 2f);

                for (int s = 0; s < SAMPLES.Length; s++)
                {
                    GameObject host = Spawn(prefab, c, out childName);
                    Color[] pixels = Bake(host, camera, target, readback, SAMPLES[s]);
                    Object.DestroyImmediate(host);

                    if (s < SHEET_COLUMNS && row < totalRows)
                    {
                        sheet.SetPixels(
                            FRAME_SIZE * s, FRAME_SIZE * (totalRows - 1 - row),
                            FRAME_SIZE, FRAME_SIZE, pixels);
                    }

                    int lit = Measure(pixels, out int w, out int h, out int minY, out int maxY);

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
                }

                row++;

                if (maxLit == 0)
                {
                    report.AppendLine($"  {childName,-14}(안 그려짐)");
                    continue;
                }

                float swing = (maxLit - minLit) * 100f / maxLit;
                float ratio = maxHeight > 0f ? maxWidth / maxHeight : 0f;
                string drift = lowest <= highest
                    ? $"{lowest:0.##}~{highest:0.##}"
                    : "-";

                report.AppendLine(
                    $"  {childName,-14}{maxWidth:0.##} x {maxHeight:0.##}".PadRight(32) +
                    $"{ratio,6:0.##}{swing,6:0}%{drift,12}");
            }

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

        report.AppendLine($"시트: {SHEET_PATH} (가로 = {string.Join(" / ", SAMPLES)}초)");

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static GameObject Spawn(GameObject prefab, int keepIndex, out string childName)
    {
        GameObject host = Object.Instantiate(prefab);
        host.transform.position = Vector3.zero;

        var systems = host.GetComponentsInChildren<ParticleSystem>(true);
        childName = systems[keepIndex].name;

        for (int i = 0; i < systems.Length; i++)
        {
            var renderer = systems[i].GetComponent<ParticleSystemRenderer>();

            if (renderer != null)
            {
                renderer.enabled = i == keepIndex;
            }

            systems[i].useAutoRandomSeed = false;
            systems[i].randomSeed = FIXED_SEED;
        }

        foreach (Transform child in host.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = MEASURE_LAYER;
        }

        return host;
    }

    private static Color[] Bake(
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

    private static int Measure(
        Color[] pixels, out int width, out int height, out int minY, out int maxY)
    {
        int lit = 0;
        int minX = FRAME_SIZE;
        int maxX = -1;
        minY = FRAME_SIZE;
        maxY = -1;

        for (int p = 0; p < pixels.Length; p++)
        {
            if (pixels[p].a <= ALPHA_THRESHOLD)
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
        }

        width = maxX >= 0 ? maxX - minX + 1 : 0;
        height = maxY >= 0 ? maxY - minY + 1 : 0;

        return lit;
    }

    private static GameObject CreateCamera(out Camera camera)
    {
        var host = new GameObject("BuffOrbSurveyCamera");
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
