using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 버프타워 이펙트 M3 - 버프 받는 타워 위에 띄울 구체 프리팹 4색을 만든다.
///
/// 원본(Assets/Imported/Vefects)은 건드리지 않고 가공본을
/// Assets/Imported/Prefabs/Aura/ 아래에 새로 만든다.
///
/// 하는 일:
///   1) Glow 02 자식만 남긴다 - 자식별 측정에서 비율 1.00, 루프 변동 0%로 압도적이었다
///   2) 색을 바꾼다. startColor만으로는 안 되고 ColorOverLifetime 그라디언트까지 돌린다
///   3) 정렬은 넣지 않는다 - 런타임이 수혜 타워의 DepthSortOrder를 따라 넣는다(M5)
///
/// 구조: 루트(빈 오브젝트) → Glow 02
/// 런타임은 루트 위치만 옮기면 된다.
/// </summary>
public static class BuildBuffOrbs
{
    private const string SOURCE_ROOT =
        "Assets/Imported/Vefects/Anime VFX URP/Shared/Particles/";

    private const string OUTPUT_ROOT = "Assets/Imported/Prefabs/Aura/";
    private const string REPORT_PATH = "output/vfx_check/buff_orbs_build.txt";

    private const string ORB_CHILD = "Glow 02";

    // 원본 Glow 02는 검정 배경 기준 지름 0.91월드다.
    // (자식 조사에서 0.4로 읽혔던 것은 투명 배경 + 알파 임계로 잰 코어만이다 -
    //  이 셰이더는 가산 계열이라 알파가 아니라 밝기로 재야 실제 크기가 나온다.)
    //
    // 주의: 발광을 올리면 옅은 가장자리가 임계값을 넘어와 **지름이 함께 커진다**
    // (_EmissionIntensity 2.4에서 0.91 → 1.18). 아래 값은 그 상태에서 다시 잰 것이다.
    // 발광을 바꾸면 이 값도 다시 재야 한다.
    private const float SOURCE_DIAMETER = 1.18f;
    private const float TARGET_DIAMETER = 0.34f;
    private const float SIZE_SCALE = TARGET_DIAMETER / SOURCE_DIAMETER;

    // 색을 1보다 크게 키워도 밝아지지 않는다 - 셰이더가 색을 saturate한다.
    // 2.6배를 걸었더니 밝기는 0.20 → 0.19로 그대로였고, 채널이 전부 1로 잘려
    // **네 색이 서로 구분되지 않게** 됐다(시간과 강화가 같은 값). 1로 둔다.
    private const float BRIGHTNESS = 1f;

    // 알파도 올릴 수 없다 - 원본 알파 키가 이미 1.0이라 어떤 배수를 걸어도 clamp된다.
    // (3.2와 4.2를 걸어 봤지만 밝기가 0.20으로 똑같았다.)
    //
    // 이 셰이더는 _Src=1 _Dst=1인 순수 가산이고, 밝기를 정하는 손잡이는 머티리얼의
    // _EmissionIntensity다. 다만 원본 M_VFX_Part_01은 **32개 프리팹이 공유**하므로
    // 그대로 고치면 다른 이펙트가 전부 밝아진다. 구체 전용 복제본을 만들어 거기에 건다.
    private const float EMISSION_INTENSITY = 2.4f;
    private const string ORB_MATERIAL_NAME = "M_BuffOrb";
    private const string EMISSION_INTENSITY_KEY = "_EmissionIntensity";

    private struct Entry
    {
        public string Source;
        public string Output;
        public Color Tint;
    }

    // 색은 팀 지정이다. Heal_Loop을 쓰는 은신만 원본이 다르다.
    private static readonly Entry[] ENTRIES =
    {
        new Entry
        {
            Source = "VFX_Buff_Loop",
            Output = "FX_BuffOrb_Time",
            Tint = new Color(1f, 0.85f, 0.15f),
        },
        new Entry
        {
            Source = "VFX_Buff_Loop",
            Output = "FX_BuffOrb_Enhancement",
            Tint = new Color(1f, 0.42f, 0.12f),
        },
        new Entry
        {
            Source = "VFX_Buff_Loop",
            Output = "FX_BuffOrb_Soul",
            Tint = new Color(0.35f, 0.75f, 1f),
        },
        new Entry
        {
            Source = "VFX_Heal_Loop",
            Output = "FX_BuffOrb_Stealth",
            Tint = new Color(0.65f, 0.35f, 1f),
        },
    };

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");

        if (!AssetDatabase.IsValidFolder(OUTPUT_ROOT.TrimEnd('/')))
        {
            AssetDatabase.CreateFolder("Assets/Imported/Prefabs", "Aura");
        }

        var report = new StringBuilder();
        report.AppendLine("[BuildBuffOrbs] 버프타워 이펙트 M3");
        report.AppendLine($"  원본 자식 {ORB_CHILD}만 남기고 색을 바꾼다");
        report.AppendLine("  정렬은 넣지 않는다 - 런타임이 DepthSortOrder를 따라 넣는다(M5)");
        report.AppendLine();

        foreach (Entry entry in ENTRIES)
        {
            string sourcePath = SOURCE_ROOT + entry.Source + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

            if (source == null)
            {
                report.AppendLine($"[없음] {sourcePath}");
                continue;
            }

            report.AppendLine($"=== {entry.Output}  ({entry.Source} / {ORB_CHILD})");

            GameObject built = Compose(source, out ParticleSystem orb);

            if (orb == null)
            {
                report.AppendLine($"  [실패] {ORB_CHILD} 자식이 없습니다");
                Object.DestroyImmediate(built);
                continue;
            }

            built.name = entry.Output;

            ParticleSystem.MainModule main = orb.main;
            main.startSize = Multiply(main.startSize, SIZE_SCALE);
            report.AppendLine(
                $"  크기 {SOURCE_DIAMETER} → {TARGET_DIAMETER} 월드 (배율 {SIZE_SCALE:0.###})");

            ApplyTint(orb, entry.Tint * BRIGHTNESS, report);
            ApplyEmission(orb, report);

            string outputPath = OUTPUT_ROOT + entry.Output + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(built, outputPath);
            Object.DestroyImmediate(built);

            report.AppendLine($"  저장 {outputPath}");
            report.AppendLine();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    // 루트(빈 오브젝트) → Glow 02. 런타임이 루트만 옮기게 하려는 구조다.
    private static GameObject Compose(GameObject source, out ParticleSystem orb)
    {
        var root = new GameObject("BuffOrb");
        root.transform.position = Vector3.zero;

        GameObject instance = Object.Instantiate(source);
        orb = null;

        foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.name == ORB_CHILD)
            {
                orb = particles;
                break;
            }
        }

        if (orb == null)
        {
            Object.DestroyImmediate(instance);
            return root;
        }

        orb.transform.SetParent(root.transform, false);
        orb.transform.localPosition = Vector3.zero;

        // 나머지 자식은 통째로 버린다 - 폭이 3월드까지 가고 루프 변동이 48~100%다.
        Object.DestroyImmediate(instance);

        return root;
    }

    // 구체 전용 머티리얼로 갈아 끼우고 발광 강도를 올린다.
    //
    // 원본 M_VFX_Part_01을 그대로 고치면 그것을 쓰는 32개 프리팹이 전부 밝아진다.
    // 복제본을 한 장 만들어(이미 있으면 재사용) 구체 넷이 공유한다 - 강도가 색과 무관하므로
    // 색마다 따로 만들 이유가 없다.
    private static void ApplyEmission(ParticleSystem orb, StringBuilder report)
    {
        var renderer = orb.GetComponent<ParticleSystemRenderer>();

        if (renderer == null || renderer.sharedMaterial == null)
        {
            report.AppendLine("  [경고] 렌더러/머티리얼이 없어 발광 강도를 못 걸었습니다");
            return;
        }

        string materialPath = OUTPUT_ROOT + ORB_MATERIAL_NAME + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        if (material == null)
        {
            material = new Material(renderer.sharedMaterial) { name = ORB_MATERIAL_NAME };
            AssetDatabase.CreateAsset(material, materialPath);
            report.AppendLine($"  머티리얼 생성 {materialPath}");
        }

        if (!material.HasProperty(EMISSION_INTENSITY_KEY))
        {
            report.AppendLine($"  [경고] {EMISSION_INTENSITY_KEY} 프로퍼티가 없습니다");
            return;
        }

        material.SetFloat(EMISSION_INTENSITY_KEY, EMISSION_INTENSITY);
        EditorUtility.SetDirty(material);

        renderer.sharedMaterial = material;
        report.AppendLine(
            $"  {ORB_MATERIAL_NAME}.{EMISSION_INTENSITY_KEY} = {EMISSION_INTENSITY}");
    }

    // 색조 대신 색을 직접 지정한다 - 원본 Glow는 거의 흰색이라 채도를 보존하는 색조 회전으로는
    // 아무 일도 일어나지 않는다(작업노트 3-3의 SubSparks와 같은 상황).
    // startColor와 ColorOverLifetime 그라디언트를 둘 다 칠한다.
    // 알파는 원본을 유지한다(이미 1.0이라 올릴 여지가 없고, 건드리면 페이드 타이밍만 바뀐다).
    // 밝기는 머티리얼의 _EmissionIntensity로 올린다.
    private static void ApplyTint(ParticleSystem orb, Color tint, StringBuilder report)
    {
        ParticleSystem.MainModule main = orb.main;
        ParticleSystem.MinMaxGradient start = main.startColor;

        report.AppendLine($"  startColor 모드 {start.mode} → {ColorText(tint)}");

        main.startColor = Tint(start, tint);

        ParticleSystem.ColorOverLifetimeModule color = orb.colorOverLifetime;

        if (!color.enabled)
        {
            report.AppendLine("  ColorOverLifetime 꺼짐 - 건너뜀");
            return;
        }

        report.AppendLine($"  ColorOverLifetime 모드 {color.color.mode}");
        color.color = Tint(color.color, tint);
    }

    private static ParticleSystem.MinMaxGradient Tint(
        ParticleSystem.MinMaxGradient source, Color tint)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(WithAlpha(tint, source.color.a));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    WithAlpha(tint, source.colorMin.a),
                    WithAlpha(tint, source.colorMax.a));

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, tint));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    TintGradient(source.gradientMin, tint),
                    TintGradient(source.gradientMax, tint));

            default:
                return source;
        }
    }

    // 색 키만 칠하고 알파 키는 그대로 둔다.
    private static Gradient TintGradient(Gradient source, Color tint)
    {
        var colorKeys = new GradientColorKey[source.colorKeys.Length];

        for (int i = 0; i < source.colorKeys.Length; i++)
        {
            colorKeys[i] = new GradientColorKey(tint, source.colorKeys[i].time);
        }

        var alphaKeys = new GradientAlphaKey[source.alphaKeys.Length];

        for (int i = 0; i < source.alphaKeys.Length; i++)
        {
            alphaKeys[i] = new GradientAlphaKey(
                source.alphaKeys[i].alpha,
                source.alphaKeys[i].time);
        }

        var result = new Gradient();
        result.SetKeys(colorKeys, alphaKeys);
        result.mode = source.mode;

        return result;
    }

    // MinMaxCurve.constant은 constantMax의 별칭이다(작업노트 3-10).
    // 모드를 보고 해당하는 쪽만 건드려야 제곱으로 걸리지 않는다.
    private static ParticleSystem.MinMaxCurve Multiply(
        ParticleSystem.MinMaxCurve curve, float factor)
    {
        return curve.mode == ParticleSystemCurveMode.TwoConstants
            ? new ParticleSystem.MinMaxCurve(curve.constantMin * factor, curve.constantMax * factor)
            : new ParticleSystem.MinMaxCurve(curve.constant * factor);
    }

    private static Color WithAlpha(Color color, float alpha) =>
        new Color(color.r, color.g, color.b, alpha);

    private static string ColorText(Color color) =>
        $"({color.r:0.##}, {color.g:0.##}, {color.b:0.##})";
}
