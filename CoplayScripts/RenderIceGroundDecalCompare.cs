using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 얼음 명중 이펙트의 바닥 데칼을 "지금(HorizontalBillboard) vs 변환(Billboard + 2:1 눌림)"으로 굽는다.
//
// HorizontalBillboard는 월드 XZ 평면에 법선 +Y로 고정돼, 회전 0으로 +Z를 보는 이 프로젝트의
// 카메라에서는 정확히 옆날로 서서 화면 폭 0으로 그려진다. 트랜스폼 회전은 이 렌더 모드에
// 아무 영향이 없으므로 루트를 눕혀도 살아나지 않는다. 렌더 모드 자체를 바꿔야 한다.
//
// 프리팹 에셋은 건드리지 않는다 - 씬에 만든 인스턴스에서만 바꿔 찍는다.
public static class RenderIceGroundDecalCompare
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";

    private static readonly float[] TIMES = { 0.04f, 0.08f, 0.14f, 0.22f, 0.4f };

    private const int PANEL = 420;
    private const float ORTHO_SIZE = 1.3f;
    private const float CAMERA_DISTANCE = 10f;

    // 씬 한복판에는 성과 용이 있어 배경이 지저분하다. 아무것도 없는 곳으로 옮겨 찍는다.
    private const float STAGE_Y = -200f;

    // 아이소메트릭 셀이 1 x 0.5(Grid.prefab m_CellSize)이므로 바닥에 누운 원은 화면에서 2:1 타원이다.
    private const float ISO_SQUASH = 0.5f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);

        if (source == null)
        {
            return $"프리팹을 찾을 수 없습니다: {IMPACT_PATH}";
        }

        var report = new StringBuilder();
        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * 2, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("IceDecalCompareCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);

        var target = new RenderTexture(PANEL, PANEL, 24);

        try
        {
            // 텍스처는 y가 위로 자라므로 위 줄의 y가 PANEL이다.
            RenderRow(sheet, camera, target, source, false, PANEL, report);
            RenderRow(sheet, camera, target, source, true, 0, report);

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "ice_decal_compare.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            report.Insert(0, $"{path}\n위 줄=지금 / 아래 줄=Billboard 변환, 시각(초): {string.Join(", ", TIMES)}\n");
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

    private static void RenderRow(
        Texture2D sheet,
        Camera camera,
        RenderTexture target,
        GameObject source,
        bool convertsDecals,
        int rowY,
        StringBuilder report)
    {
        for (int index = 0; index < TIMES.Length; index++)
        {
            var instance = Object.Instantiate(source);
            instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

            if (convertsDecals)
            {
                ConvertDecals(instance, index == 0 ? report : null);
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

            sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

            Object.DestroyImmediate(panel);
            Object.DestroyImmediate(instance);
        }
    }

    // HorizontalBillboard 이미터만 골라 카메라를 보게 하고, 세로만 눌러 바닥에 누운 타원으로 만든다.
    // VerticalBillboard(솟는 기둥·연기)는 지금 카메라에서 이미 제대로 나오므로 건드리지 않는다.
    private static void ConvertDecals(GameObject root, StringBuilder report)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null || renderer.renderMode != ParticleSystemRenderMode.HorizontalBillboard)
            {
                continue;
            }

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;

            var main = ps.main;
            ParticleSystem.MinMaxCurve width = main.startSizeX;

            main.startSize3D = true;
            main.startSizeY = Scaled(width, ISO_SQUASH);
            main.startSizeZ = width;

            if (report != null)
            {
                float lossy = ps.transform.lossyScale.x;
                report.Append("변환: ").Append(ps.gameObject.name.PadRight(14));
                report.Append(" startSize=").Append(width.constant.ToString("0.##").PadRight(6));
                report.Append(" lossyScale=").Append(lossy.ToString("0.###").PadRight(8));
                report.Append(" 화면폭=").Append((width.constant * lossy).ToString("0.##"));
                report.AppendLine();
            }
        }
    }

    private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve source, float multiplier)
    {
        switch (source.mode)
        {
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(
                    source.constantMin * multiplier, source.constantMax * multiplier);

            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curve);

            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curveMin, source.curveMax);

            default:
                return new ParticleSystem.MinMaxCurve(source.constant * multiplier);
        }
    }

    private static readonly string[] DECAL_NAMES = { "nova", "glow", "ground", "blue_crack" };

    // 데칼을 하나씩만 남기고 굽는다. 합쳐 놓으면 무엇이 새로 보이는지 구분이 안 된다.
    // 줄 순서는 DECAL_NAMES 그대로, 각 줄의 왼쪽이 지금 / 오른쪽이 변환 후다.
    public static string ExecuteIsolated()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);

        if (source == null)
        {
            return $"프리팹을 찾을 수 없습니다: {IMPACT_PATH}";
        }

        var sheet = new Texture2D(PANEL * TIMES.Length * 2, PANEL * DECAL_NAMES.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("IceDecalIsolateCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);

        var target = new RenderTexture(PANEL, PANEL, 24);

        try
        {
            for (int row = 0; row < DECAL_NAMES.Length; row++)
            {
                // 텍스처는 y가 위로 자라므로 첫 이름이 맨 윗줄에 오도록 뒤집는다.
                int rowY = (DECAL_NAMES.Length - 1 - row) * PANEL;

                for (int half = 0; half < 2; half++)
                {
                    for (int index = 0; index < TIMES.Length; index++)
                    {
                        var instance = Object.Instantiate(source);
                        instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                        IsolateDecal(instance, DECAL_NAMES[row]);

                        if (half == 1)
                        {
                            ConvertDecals(instance, null);
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

                        int column = half * TIMES.Length + index;
                        sheet.SetPixels(column * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

                        Object.DestroyImmediate(panel);
                        Object.DestroyImmediate(instance);
                    }
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "ice_decal_isolated.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            return $"{path}\n줄(위→아래): {string.Join(", ", DECAL_NAMES)}\n" +
                   $"왼쪽 {TIMES.Length}칸=지금 / 오른쪽 {TIMES.Length}칸=변환, 시각(초): {string.Join(", ", TIMES)}";
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

    private static void IsolateDecal(GameObject root, string keptName)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer != null && ps.gameObject.name != keptName)
            {
                renderer.enabled = false;
            }
        }
    }
}
