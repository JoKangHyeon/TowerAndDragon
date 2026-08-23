using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 석궁 타워의 명중 이펙트 후보를 화살 타워 최종본과 같은 시트에 놓고 굽는다.
//
// 석궁은 화살과 같은 결이어야 한다 - 일직선 궤적에 과하지 않은 명중. 그래서 기준을
// 얼음·암석이 아니라 <b>화살 타워의 현재 임팩트</b>로 잡는다.
//
// 지금 석궁은 Impact_V1_21_red_arrow에서 Lightning·SparksUp을 뺀 것인데 너무 단조롭다는
// 판단이 나왔다. 무엇을 뺐길래 단조로워졌는지 보려고 원본도 같이 굽는다.
//
// 화살 계열 임팩트는 지면 데칼이 아니라 표적에 꽂히는 그림이라, 얼음·암석에 걸었던
// HorizontalBillboard 눕히기를 걸지 않는다. 이 이펙트들은 회전을 발사각에 맞춰 쓰기 때문에
// (RotatesImpactToTravelDirection 기본값 true) 바닥에 눕히면 오히려 틀린다.
//
// 프리팹 에셋은 건드리지 않는다. 씬에 만든 인스턴스에서만 끄고 켠다.
public static class RenderCrossBowCandidates
{
    private const string VOL1 = "Assets/Imported/Prefabs/Projectile/AAA_Vol1";
    private const string TOWER = "Assets/Imported/Prefabs/Projectile/Tower";

    private sealed class Row
    {
        public string Label;
        public string Path;
        public float Scale = 1f;

        // 채우면 그 이름들의 렌더러를 끈다. 지금 석궁이 무엇을 빼고 있는지 재현할 때 쓴다.
        public string[] DisabledChildren;
    }

    // 후보는 전부 석궁이 지금 쓰는 배수(0.55)로 굽는다. 배수를 섞으면 어느 것이
    // 그림이 좋아서 큰 건지 그냥 크게 찍어서 큰 건지 구분이 안 된다.
    private const float CANDIDATE_SCALE = 0.55f;

    private static readonly Row[] ROWS =
    {
        new Row { Label = "화살 (현재·기준)", Path = TOWER + "/Impact_Tower_Arrow.prefab" },
        new Row { Label = "석궁 (현재)", Path = TOWER + "/Impact_Tower_CrossBow.prefab" },
        new Row
        {
            Label = "21_red_arrow 원본",
            Path = VOL1 + "/Impact_V1_21_red_arrow.prefab",
            Scale = CANDIDATE_SCALE
        },
        new Row
        {
            Label = "04_yellow_arrow",
            Path = VOL1 + "/Impact_V1_04_yellow_arrow.prefab",
            Scale = CANDIDATE_SCALE
        },
        new Row
        {
            Label = "11_orange_arrow",
            Path = VOL1 + "/Impact_V1_11_orange_arrow.prefab",
            Scale = CANDIDATE_SCALE
        },
        new Row
        {
            Label = "20_pink_arrow",
            Path = VOL1 + "/Impact_V1_20_pink_arrow.prefab",
            Scale = CANDIDATE_SCALE
        }
    };

    // 화살 계열은 수명이 0.2~0.26초라 앞쪽을 촘촘히 본다.
    private static readonly float[] TIMES = { 0.03f, 0.06f, 0.1f, 0.16f, 0.24f };

    private const int PANEL = 360;
    private const float ORTHO_SIZE = 0.7f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    private const float LUMINANCE_THRESHOLD = 0.12f;

    private static readonly Color BACKGROUND = new Color(0.06f, 0.07f, 0.09f, 1f);
    private static readonly Color CENTER_LINE = new Color(0.35f, 0.35f, 0.4f, 1f);

    public static string Execute()
    {
        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * ROWS.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("CrossBowCamera");
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
            for (int row = 0; row < ROWS.Length; row++)
            {
                Row entry = ROWS[row];
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Path);

                report.Append("== ").AppendLine(entry.Label);

                if (source == null)
                {
                    report.AppendLine("  프리팹을 찾을 수 없습니다");
                    continue;
                }

                report.Append("  자식: ").AppendLine(string.Join(", ", ChildNames(source)));

                int rowY = (ROWS.Length - 1 - row) * PANEL;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);
                    instance.transform.localScale *= entry.Scale;

                    if (entry.DisabledChildren != null)
                    {
                        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var renderer = ps.GetComponent<ParticleSystemRenderer>();

                            if (renderer != null &&
                                Array.IndexOf(entry.DisabledChildren, ps.gameObject.name) >= 0)
                            {
                                renderer.enabled = false;
                            }
                        }
                    }

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
                    DrawCenterLine(panel);

                    sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

                    UnityEngine.Object.DestroyImmediate(panel);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "crossbow_candidates.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            var labels = new List<string>();

            foreach (Row entry in ROWS)
            {
                labels.Add(entry.Label);
            }

            report.AppendLine();
            report.AppendLine(path);
            report.Append("줄(위→아래): ").AppendLine(string.Join(", ", labels));
            report.Append("칸(왼→오른) 시각(초): ").AppendLine(string.Join(", ", TIMES));
            report.AppendLine("후보는 전부 배수 " + CANDIDATE_SCALE + "로 구웠다. 기준 두 줄은 이미 배수가 먹은 최종본이다.");

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

        report.Append("  폭=").Append(((maxX - minX + 1) * perPixel).ToString("0.##"));
        report.Append("  높이=").Append(((maxY - minY + 1) * perPixel).ToString("0.##"));
        report.AppendLine("  픽셀=" + lit);
    }

    private static void DrawCenterLine(Texture2D panel)
    {
        int y = PANEL / 2;

        for (int x = 0; x < PANEL; x++)
        {
            panel.SetPixel(x, y, CENTER_LINE);
        }

        panel.Apply();
    }
}
