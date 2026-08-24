using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 빙결(dragon_ice_freeze) 지속 표시에 쓸 원본 후보를 굽는다.
//
// 빙결은 <b>완전 정지</b>다(MonsterStatusReceiver.IsActionBlocked). 둔화(dragon_ice_slow, 속도 0.5배)와
// 별개 상태이므로 연출도 갈라야 한다 - 빙결은 "굳었다", 둔화는 "느려졌다"로 읽혀야 한다.
//
// 명중 이펙트와 달리 지속 표시는 loop=1이어야 한다. Eric 팩의 얼음 계열은 Crack 뿐이라 전부
// 버스트(일회성)여서 쓸 수 없다. 그래서 다른 팩까지 훑어 지속형만 후보로 올렸다.
//
// 사용자 제안인 "불꽃을 파랗게"도 같이 굽는다. 색조만 돌려 실제로 얼음으로 읽히는지,
// 아니면 푸른 불꽃으로 남는지는 눈으로 봐야 판단할 수 있다.
//
// 몬스터에 붙는 연출이라 스폰 높이(0)를 발밑으로 보고, 위로 얼마나 올라가는지를 함께 잰다.
//
// 프리팹 에셋은 건드리지 않는다. 씬에 만든 인스턴스에서만 색을 바꾼다.
public static class RenderFreezeCandidates
{
    private const string VEFECTS = "Assets/Imported/Vefects/Anime VFX URP/Shared/Particles";
    private const string STATUS = "Assets/Imported/Prefabs/Effects/Status";
    private const string ICE_IMPACT = "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";

    private sealed class Row
    {
        public string Label;
        public string Path;
        public float Scale = 1f;

        // 0 이상이면 그 각도로 색조를 돌린다. 음수면 원본색.
        public float HueDegrees = -1f;

        // 채우면 그 이름들만 켜고 나머지 렌더러는 끈다.
        public string[] OnlyChildren;

        // 켜면 모든 자식을 loop=1로 바꾼다. 일회성 이펙트를 지속 표시로 쓸 수 있는지 볼 때 쓴다.
        public bool ForcesLoop;
    }

    // 얼음 하늘색. 명중 이펙트(FX_Impact_IceCrack)의 blue_crack이 (0.33,0.74,1)로 색조 200도쯤이라
    // 거기에 맞춘다 - 같은 속성이 화면에서 같은 색으로 읽혀야 한다.
    private const float ICE_HUE = 200f;

    private static readonly Row[] ROWS =
    {
        // 만들어진 결과물 둘. 색조·크기·loop가 이미 프리팹에 박혀 있으므로 여기서는 손대지 않는다.
        new Row { Label = "빙결 FX_Status_IceFreeze", Path = STATUS + "/FX_Status_IceFreeze.prefab" },
        new Row { Label = "둔화 FX_Status_IceSlow", Path = STATUS + "/FX_Status_IceSlow.prefab" },

        // 대조군. 화상과 나란히 놓아 셋이 한 세트로 보이는지 본다.
        new Row { Label = "[대조] 화상 FX_Status_FireBurn", Path = STATUS + "/FX_Status_FireBurn.prefab" }
    };

    // 지속형이라 앞쪽보다 "계속 돌 때" 그림이 중요하다. 루프가 자리를 잡은 뒤를 본다.
    // 빙결은 3초다. 걸리자마자 보여야 하므로 앞쪽(0.15)부터, 끝까지 유지되는지 2.5까지 본다.
    // 빙결·둔화 모두 3초다. 걸린 순간(0.15)부터 끝(3.0)까지 본다.
    private static readonly float[] TIMES = { 0.15f, 0.5f, 1.2f, 2f, 3f };

    private const int PANEL = 360;
    private const float ORTHO_SIZE = 0.8f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    private const float LUMINANCE_THRESHOLD = 0.12f;

    private static readonly Color BACKGROUND = new Color(0.06f, 0.07f, 0.09f, 1f);
    private static readonly Color GROUND_LINE = new Color(0.35f, 0.35f, 0.4f, 1f);

    public static string Execute()
    {
        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * ROWS.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("FreezeCamera");
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
                    report.AppendLine("  프리팹을 찾을 수 없습니다: " + entry.Path);
                    continue;
                }

                int rowY = (ROWS.Length - 1 - row) * PANEL;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    GameObject instance = UnityEngine.Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);
                    instance.transform.localScale *= entry.Scale;

                    Prepare(instance, entry);

                    if (entry.HueDegrees >= 0f)
                    {
                        ApplyHue(instance, entry.HueDegrees);
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
                    DrawGroundLine(panel);

                    sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

                    UnityEngine.Object.DestroyImmediate(panel);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "freeze_candidates.png");
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
            report.AppendLine("가로선은 스폰 높이(0) - 몬스터 발밑으로 보면 된다.");

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

    private static void Prepare(GameObject instance, Row entry)
    {
        foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            bool isRoot = ps.gameObject == instance;

            if (entry.OnlyChildren != null && !isRoot &&
                Array.IndexOf(entry.OnlyChildren, ps.gameObject.name) < 0)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                continue;
            }

            if (entry.ForcesLoop)
            {
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
            }
        }
    }

    // AssignTowerProjectiles와 같은 방식으로 돌린다 - startColor와 ColorOverLifetime 둘 다.
    // 한쪽만 돌리면 색이 그라디언트에 박힌 자식이 원래 색으로 남는다(화살 궤적에서 겪었다).
    private static void ApplyHue(GameObject root, float hueDegrees)
    {
        float hue = Mathf.Repeat(hueDegrees, 360f) / 360f;

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = ShiftHue(main.startColor, hue);

            var overLifetime = ps.colorOverLifetime;

            if (overLifetime.enabled)
            {
                overLifetime.color = ShiftHue(overLifetime.color, hue);
            }
        }
    }

    private static ParticleSystem.MinMaxGradient ShiftHue(ParticleSystem.MinMaxGradient source, float hue)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ShiftHue(source.color, hue));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    ShiftHue(source.colorMin, hue), ShiftHue(source.colorMax, hue));

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(ShiftHue(source.gradient, hue));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    ShiftHue(source.gradientMin, hue), ShiftHue(source.gradientMax, hue));

            default:
                return source;
        }
    }

    private static Gradient ShiftHue(Gradient source, float hue)
    {
        if (source == null)
        {
            return null;
        }

        GradientColorKey[] colorKeys = source.colorKeys;

        for (int i = 0; i < colorKeys.Length; i++)
        {
            colorKeys[i].color = ShiftHue(colorKeys[i].color, hue);
        }

        var shifted = new Gradient();
        shifted.mode = source.mode;
        shifted.SetKeys(colorKeys, source.alphaKeys);

        return shifted;
    }

    private static Color ShiftHue(Color source, float hue)
    {
        Color.RGBToHSV(source, out float _, out float saturation, out float value);
        Color shifted = Color.HSVToRGB(hue, saturation, value);
        shifted.a = source.a;

        return shifted;
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
        report.Append("  밑=").Append(((minY - PANEL * 0.5f) * perPixel).ToString("0.##"));
        report.Append("  위=").Append(((maxY - PANEL * 0.5f) * perPixel).ToString("0.##"));
        report.AppendLine("  픽셀=" + lit);
    }

    private static void DrawGroundLine(Texture2D panel)
    {
        int y = PANEL / 2;

        for (int x = 0; x < PANEL; x++)
        {
            panel.SetPixel(x, y, GROUND_LINE);
        }

        panel.Apply();
    }
}
