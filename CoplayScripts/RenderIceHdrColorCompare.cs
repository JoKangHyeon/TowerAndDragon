using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 가산합성 글로우 레이어의 Start Color를 1 초과(HDR)로 올렸을 때를 배수별로 굽는다.
//
// 블룸(풀스크린 패스)은 씬·URP 에셋을 건드려야 해서 쓸 수 없다. 대신 이 팩의 셰이더
// Eric/URP_AdditiveFlow가 146행에서 정점 색을 그대로 곱하고(Blend One One) 있어,
// 파티클 색만 올려도 같은 방향의 밝기를 얻는다. 머티리얼은 외부 번들이라 손대지 않는다.
//
// 프리팹 에셋은 건드리지 않는다 - 씬에 만든 인스턴스에서만 바꿔 찍는다.
public static class RenderIceHdrColorCompare
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";

    // 되살린 데칼 중 가산합성인 것들. ground는 URP_AlphaBlendFlow라 제외한다.
    private static readonly string[] BOOSTED_NAMES = { "glow", "nova", "blue_crack" };

    private static readonly float[] MULTIPLIERS = { 1f, 2f, 3f, 5f };

    private static readonly float[] TIMES = { 0.04f, 0.08f, 0.14f, 0.22f, 0.4f };

    private const int PANEL = 420;
    private const float ORTHO_SIZE = 1.3f;
    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);

        if (source == null)
        {
            return $"프리팹을 찾을 수 없습니다: {IMPACT_PATH}";
        }

        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * MULTIPLIERS.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("IceHdrCompareCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        camera.allowHDR = true;

        var target = new RenderTexture(PANEL, PANEL, 24, RenderTextureFormat.DefaultHDR);

        try
        {
            for (int row = 0; row < MULTIPLIERS.Length; row++)
            {
                // 텍스처는 y가 위로 자라므로 첫 배수가 맨 윗줄에 오도록 뒤집는다.
                int rowY = (MULTIPLIERS.Length - 1 - row) * PANEL;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    var instance = Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                    Boost(instance, MULTIPLIERS[row], row == 0 && index == 0 ? report : null);

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

                    sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

                    Object.DestroyImmediate(panel);
                    Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "ice_hdr_compare.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            report.Insert(0, $"{path}\n줄(위→아래) 배수: {string.Join(", ", MULTIPLIERS)}\n" +
                             $"칸(왼→오른) 시각(초): {string.Join(", ", TIMES)}\n" +
                             $"대상: {string.Join(", ", BOOSTED_NAMES)}\n");
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

    private static void Boost(GameObject root, float multiplier, StringBuilder report)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (System.Array.IndexOf(BOOSTED_NAMES, ps.gameObject.name) < 0)
            {
                continue;
            }

            var main = ps.main;
            ParticleSystem.MinMaxGradient startColor = main.startColor;

            // 알파는 그대로 두고 RGB만 올린다. 알파를 건드리면 페이드 아웃 타이밍이 같이 바뀐다.
            switch (startColor.mode)
            {
                case ParticleSystemGradientMode.Color:
                    main.startColor = Scaled(startColor.color, multiplier);
                    break;

                case ParticleSystemGradientMode.TwoColors:
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        Scaled(startColor.colorMin, multiplier), Scaled(startColor.colorMax, multiplier));
                    break;

                default:
                    if (report != null)
                    {
                        report.AppendLine($"  건너뜀(그라디언트 모드): {ps.gameObject.name}");
                    }

                    break;
            }
        }
    }

    private static Color Scaled(Color source, float multiplier)
    {
        return new Color(source.r * multiplier, source.g * multiplier, source.b * multiplier, source.a);
    }
}
