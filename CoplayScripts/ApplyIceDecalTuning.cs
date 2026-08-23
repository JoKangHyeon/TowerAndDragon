using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 얼음 명중 이펙트의 바닥 데칼 튜닝을 프리팹에 반영한다. 세 가지를 한 번에 한다.
//
// 1) HorizontalBillboard -> Billboard : 월드 XZ 평면에 고정된 렌더 모드라 회전 0인 이 프로젝트의
//    카메라에서는 옆날로 서서 아무것도 그리지 않았다. 트랜스폼 회전으로는 못 살린다.
// 2) 3D Start Size로 세로만 절반 : 아이소메트릭 셀이 1 x 0.5(Grid.prefab)이라 바닥에 누운 원은
//    화면에서 2:1 타원이다.
// 3) 가산합성 레이어의 Start Color를 HDR(1 초과)로 : 블룸을 쓸 수 없어서(씬·URP 에셋 수정 금지)
//    대신 쓴다. Eric/URP_AdditiveFlow가 정점 색을 그대로 곱하고 Blend One One이라 그만큼 더해진다.
//    머티리얼은 외부 번들이라 손대지 않는다.
//
// rock(솟는 얼음 창)은 건드리지 않는다 - 셀에 맞춰 정렬해 둔 크기다.
//
// 모든 값을 원본 팩 프리팹에서 다시 계산한다. 현재 값에 곱하면 두 번 돌릴 때마다 커져서
// 배수를 바꿔 다시 맞출 수도, 되돌릴 수도 없다.
//
// 이것은 검증용 손수정이다. BuildStatusVfx를 다시 돌리면 사라진다.
public static class ApplyIceDecalTuning
{
    private const string BASELINE_PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_GroundCrack_Blue.prefab";

    private static readonly string[] TARGET_PATHS =
    {
        "Assets/Imported/Prefabs/Effects/Status/FX_Impact_IceCrack.prefab",
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab"
    };

    // 아이소메트릭 셀이 1 x 0.5(Grid.prefab m_CellSize)이므로 바닥에 누운 원은 화면에서 2:1 타원이다.
    private const float ISO_SQUASH = 0.5f;

    // 후보 D. 회색 먼지는 줄이고 파란 균열을 주인공으로 둔다 -
    // 먼지를 같이 키우면 0.14초쯤에 허연 도넛이 얼음 창을 덮는다.
    private const float COLOR_MULTIPLIER = 3f;

    private static readonly Dictionary<string, float> SIZE_MULTIPLIERS = new Dictionary<string, float>
    {
        { "glow", 2f }, { "nova", 2f }, { "blue_crack", 3f }, { "ground", 0.5f }
    };

    // 그중 가산합성이라 색을 올릴 수 있는 것들. ground는 URP_AlphaBlendFlow라 제외한다.
    private static readonly string[] BOOSTED_NAMES = { "glow", "nova", "blue_crack" };

    public static string Execute()
    {
        return Run(true);
    }

    public static string Revert()
    {
        return Run(false);
    }

    private static string Run(bool applies)
    {
        GameObject baseline = AssetDatabase.LoadAssetAtPath<GameObject>(BASELINE_PATH);

        if (baseline == null)
        {
            return $"원본 프리팹을 찾을 수 없습니다: {BASELINE_PATH}";
        }

        var report = new StringBuilder();
        report.AppendLine(applies
            ? $"적용 - 색 {COLOR_MULTIPLIER}배, 크기 {Describe()}"
            : "되돌림 - 원본 팩 값으로");

        foreach (string path in TARGET_PATHS)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents == null)
            {
                report.AppendLine($"프리팹을 찾을 수 없습니다: {path}");
                continue;
            }

            foreach (KeyValuePair<string, float> entry in SIZE_MULTIPLIERS)
            {
                ParticleSystem original = Find(baseline, entry.Key);
                ParticleSystem target = Find(contents, entry.Key);

                if (original == null || target == null)
                {
                    report.AppendLine($"  찾지 못함: {entry.Key}");
                    continue;
                }

                var originalMain = original.main;
                var main = target.main;
                var renderer = target.GetComponent<ParticleSystemRenderer>();

                float sizeMultiplier = applies ? entry.Value : 1f;
                float colorMultiplier =
                    applies && System.Array.IndexOf(BOOSTED_NAMES, entry.Key) >= 0 ? COLOR_MULTIPLIER : 1f;

                ParticleSystem.MinMaxCurve width = ScaledCurve(originalMain.startSizeX, sizeMultiplier);

                if (applies)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.alignment = ParticleSystemRenderSpace.View;

                    main.startSize3D = true;
                    main.startSizeX = width;
                    main.startSizeY = ScaledCurve(width, ISO_SQUASH);
                    main.startSizeZ = width;
                }
                else
                {
                    renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                    renderer.alignment = original.GetComponent<ParticleSystemRenderer>().alignment;

                    main.startSize3D = false;
                    main.startSize = width;
                }

                // 알파는 그대로 둔다. 알파를 건드리면 페이드 아웃 타이밍이 같이 바뀐다.
                Color source = originalMain.startColor.color;
                main.startColor = new Color(
                    source.r * colorMultiplier,
                    source.g * colorMultiplier,
                    source.b * colorMultiplier,
                    source.a);

                report.Append("  ").Append(entry.Key.PadRight(12));
                report.Append("크기 ").Append(originalMain.startSizeX.constant.ToString("0.##"));
                report.Append(" -> ").Append(width.constant.ToString("0.##").PadRight(8));
                report.Append("색 x").AppendLine(colorMultiplier.ToString("0.##"));
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            report.AppendLine($"  저장: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    private static string Describe()
    {
        var parts = new List<string>();

        foreach (KeyValuePair<string, float> entry in SIZE_MULTIPLIERS)
        {
            parts.Add($"{entry.Key} x{entry.Value:0.##}");
        }

        return string.Join(", ", parts);
    }

    private static ParticleSystem Find(GameObject root, string name)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == name)
            {
                return ps;
            }
        }

        return null;
    }

    private static ParticleSystem.MinMaxCurve ScaledCurve(ParticleSystem.MinMaxCurve source, float multiplier)
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
}
