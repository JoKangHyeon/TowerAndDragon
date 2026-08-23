using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 발사체를 실제로 날려 보면서 궤적을 굽는다.
//
// 제자리에서 Simulate만 하면 궤적을 볼 수 없다. World 공간 파티클은 방출된 자리에 남고
// 트랜스폼만 앞으로 가야 뒤에 꼬리가 생기는데, 제자리에 두면 한 점에 전부 겹쳐 쌓인다.
// 그래서 한 프레임씩 Simulate하면서 그 사이에 위치를 옮긴다.
//
// 궤적 두께를 눈이 아니라 픽셀로 재려고 만들었다. "흐릿하다"는 크기 문제일 수도, 알파나
// 방출량 문제일 수도 있어서 startSize만 보고는 판단할 수 없다.
//
// 함께 각 시스템의 simulationSpace도 찍는다. 프리팹 YAML에는 기본값이 생략돼 있어
// 파일을 긁어서는 알 수 없고, 이 값이 곧 AssignTowerProjectiles가 "궤적"으로 취급하는 기준이다.
//
// 프리팹 에셋은 건드리지 않는다. 씬에 만든 인스턴스만 옮긴다.
public static class RenderProjectileTrails
{
    private sealed class Trial
    {
        public string Label;
        public string Path;

        // 0이 아니면 Stretch 시스템의 길이배율을 이 값으로 덮어쓴다.
        // 석궁 SubEnergy는 -1.5(음수)라 꼬리가 진행 방향 <b>반대로</b> 늘어난다 -
        // 뒤에 끌리는 궤적이 아니라 앞으로 삐죽 튀어나오는 조각이 된다.
        public float LengthScaleOverride;

        // 0이 아니면 World 공간 시스템의 startSize에 이 배수를 곱한다.
        // 정의에 TrailSizeMultiplier를 넣기 전에 여기서 먼저 재 본다.
        public float TrailSizeOverride;
    }

    private static readonly Trial[] TARGETS =
    {
        new Trial
        {
            Label = "화살 (기준)",
            Path = "Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_Arrow.prefab"
        },
        new Trial
        {
            Label = "석궁 (01_nature_arrow, 색조 60)",
            Path = "Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_CrossBow.prefab"
        }
    };

    // 화면 왼쪽에서 오른쪽으로 가로질러 날린다. 카메라가 담는 폭보다 조금 짧게 잡아
    // 시작점과 끝점이 모두 화면 안에 들어오게 한다.
    private const float TRAVEL_DISTANCE = 3f;
    private const float TRAVEL_SECONDS = 0.4f;

    // 60fps에 맞춘다. 실제 게임과 같은 간격으로 방출돼야 궤적 밀도가 같아진다.
    private const float FRAME_SECONDS = 1f / 60f;

    private const int PANEL_WIDTH = 900;
    private const int PANEL_HEIGHT = 300;
    private const float ORTHO_SIZE = 0.7f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    private const float LUMINANCE_THRESHOLD = 0.12f;

    private static readonly Color BACKGROUND = new Color(0.06f, 0.07f, 0.09f, 1f);

    public static string Execute()
    {
        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL_WIDTH, PANEL_HEIGHT * TARGETS.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("TrailCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BACKGROUND;
        camera.allowHDR = true;
        camera.aspect = (float)PANEL_WIDTH / PANEL_HEIGHT;

        var target = new RenderTexture(PANEL_WIDTH, PANEL_HEIGHT, 24, RenderTextureFormat.DefaultHDR);

        try
        {
            for (int row = 0; row < TARGETS.Length; row++)
            {
                Trial trial = TARGETS[row];
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(trial.Path);

                report.Append("== ").AppendLine(trial.Label);

                if (source == null)
                {
                    report.AppendLine("  프리팹을 찾을 수 없습니다");
                    continue;
                }

                GameObject instance = UnityEngine.Object.Instantiate(source);

                try
                {
                    ApplyOverrides(instance, trial);
                    DumpSpaces(instance, report);
                    Fly(instance);

                    camera.targetTexture = target;
                    camera.Render();

                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;

                    var panel = new Texture2D(PANEL_WIDTH, PANEL_HEIGHT, TextureFormat.RGBA32, false);
                    panel.ReadPixels(new Rect(0f, 0f, PANEL_WIDTH, PANEL_HEIGHT), 0, 0);
                    panel.Apply();

                    RenderTexture.active = previous;

                    MeasureTrail(panel, report);

                    int rowY = (TARGETS.Length - 1 - row) * PANEL_HEIGHT;
                    sheet.SetPixels(0, rowY, PANEL_WIDTH, PANEL_HEIGHT, panel.GetPixels());

                    UnityEngine.Object.DestroyImmediate(panel);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }

                report.AppendLine();
            }

            sheet.Apply();

            string sheetPath = Path.Combine(Path.GetTempPath(), "projectile_trails.png");
            File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());

            var labels = new List<string>();

            foreach (Trial trial in TARGETS)
            {
                labels.Add(trial.Label);
            }

            report.AppendLine(sheetPath);
            report.Append("줄(위→아래): ").AppendLine(string.Join(", ", labels));
            report.AppendLine("왼쪽에서 오른쪽으로 날아간 뒤의 한 장이다.");

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

    private static void ApplyOverrides(GameObject instance, Trial trial)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null)
            {
                continue;
            }

            if (trial.LengthScaleOverride != 0f &&
                renderer.renderMode == ParticleSystemRenderMode.Stretch)
            {
                renderer.lengthScale = trial.LengthScaleOverride;
            }

            ParticleSystem.MainModule main = ps.main;

            if (trial.TrailSizeOverride != 0f &&
                main.simulationSpace == ParticleSystemSimulationSpace.World)
            {
                main.startSize = new ParticleSystem.MinMaxCurve(
                    main.startSize.constant * trial.TrailSizeOverride);
            }
        }
    }

    // 어느 시스템이 궤적으로 취급되는지 남긴다. AssignTowerProjectiles.ApplyTrailSize가
    // simulationSpace == World인 것에만 배수를 곱하므로, 이 목록이 곧 배수가 먹은 대상이다.
    private static void DumpSpaces(GameObject instance, StringBuilder report)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            var emission = ps.emission;

            report.Append("   ").Append(ps.gameObject.name.PadRight(30));
            report.Append(main.simulationSpace.ToString().PadRight(8));
            report.Append("크기=").Append(main.startSize.constant.ToString("0.###").PadRight(8));
            report.Append("수명=").Append(main.startLifetime.constant.ToString("0.##").PadRight(7));
            report.Append("방출/초=").Append(emission.rateOverTime.constant.ToString("0.#").PadRight(8));
            report.AppendLine(main.simulationSpace == ParticleSystemSimulationSpace.World
                ? "<- 궤적(배수 적용됨)"
                : "");
        }
    }

    private static void Fly(GameObject instance)
    {
        int frames = Mathf.RoundToInt(TRAVEL_SECONDS / FRAME_SECONDS);
        float step = TRAVEL_DISTANCE / frames;
        float startX = -TRAVEL_DISTANCE * 0.5f;

        instance.transform.position = new Vector3(startX, STAGE_Y, 0f);

        // 첫 프레임은 방출만 시키고 옮기지 않는다. 옮기고 나서 시뮬레이션하면
        // 첫 입자가 이미 한 칸 앞에서 태어나 꼬리 시작점이 어긋난다.
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Simulate(FRAME_SECONDS, false, true, true);
        }

        for (int i = 1; i <= frames; i++)
        {
            instance.transform.position = new Vector3(startX + step * i, STAGE_Y, 0f);

            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Simulate(FRAME_SECONDS, false, false, true);
            }
        }
    }

    // 궤적의 두께는 세로로 잰다. 가로는 날아간 거리라 두께와 무관하다.
    // 열마다 밝은 픽셀의 세로 범위를 재고, 그중 가운데값을 대표 두께로 삼는다 -
    // 평균을 쓰면 머리의 밝은 덩어리가 꼬리를 끌어올려 실제보다 두껍게 나온다.
    private static void MeasureTrail(Texture2D panel, StringBuilder report)
    {
        Color[] pixels = panel.GetPixels();
        var thicknesses = new List<float>();
        int lit = 0;
        int litColumns = 0;

        float perPixel = ORTHO_SIZE * 2f / PANEL_HEIGHT;

        for (int x = 0; x < PANEL_WIDTH; x++)
        {
            int minY = PANEL_HEIGHT;
            int maxY = -1;

            for (int y = 0; y < PANEL_HEIGHT; y++)
            {
                Color c = pixels[y * PANEL_WIDTH + x];

                if (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f <= LUMINANCE_THRESHOLD)
                {
                    continue;
                }

                lit++;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            if (maxY < 0)
            {
                continue;
            }

            litColumns++;
            thicknesses.Add((maxY - minY + 1) * perPixel);
        }

        if (thicknesses.Count == 0)
        {
            report.AppendLine("  (아무것도 그려지지 않음)");
            return;
        }

        thicknesses.Sort();

        float median = thicknesses[thicknesses.Count / 2];
        float thickest = thicknesses[thicknesses.Count - 1];
        float length = litColumns * (ORTHO_SIZE * 2f * PANEL_WIDTH / PANEL_HEIGHT) / PANEL_WIDTH;

        report.Append("  두께 가운데값=").Append(median.ToString("0.###"));
        report.Append("  최대=").Append(thickest.ToString("0.###"));
        report.Append("  길이=").Append(length.ToString("0.##"));
        report.AppendLine("  픽셀=" + lit);
    }
}
