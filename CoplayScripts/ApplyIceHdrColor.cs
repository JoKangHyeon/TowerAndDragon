using System.Text;
using UnityEditor;
using UnityEngine;

// 가산합성 글로우 레이어의 Start Color를 HDR(1 초과)로 올려 프리팹에 반영한다.
//
// 블룸을 쓸 수 없어서(씬·URP 에셋 수정 금지) 대신 쓰는 방법이다. 이 팩의 셰이더
// Eric/URP_AdditiveFlow는 정점 색을 그대로 곱하고 Blend One One이라, 파티클 색만 올려도
// 밝기가 그만큼 더해진다. 머티리얼은 외부 번들이라 손대지 않는다.
//
// 배수는 원본 팩 프리팹의 색에 곱한다 - 현재 값에 곱하면 두 번 돌릴 때마다 밝아져서
// 되돌릴 수도, 배수를 바꿔 다시 맞출 수도 없다.
public static class ApplyIceHdrColor
{
    private const string BASELINE_PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_GroundCrack_Blue.prefab";

    private static readonly string[] TARGET_PATHS =
    {
        "Assets/Imported/Prefabs/Effects/Status/FX_Impact_IceCrack.prefab",
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab"
    };

    // 되살린 데칼 중 가산합성인 것들. ground는 URP_AlphaBlendFlow라 제외한다.
    private static readonly string[] BOOSTED_NAMES = { "glow", "nova", "blue_crack" };

    private const float MULTIPLIER = 3f;

    public static string Execute()
    {
        return Run(MULTIPLIER);
    }

    public static string Revert()
    {
        return Run(1f);
    }

    private static string Run(float multiplier)
    {
        GameObject baseline = AssetDatabase.LoadAssetAtPath<GameObject>(BASELINE_PATH);

        if (baseline == null)
        {
            return $"원본 프리팹을 찾을 수 없습니다: {BASELINE_PATH}";
        }

        var report = new StringBuilder();
        report.AppendLine($"배수 {multiplier} (원본 색 기준)");

        foreach (string path in TARGET_PATHS)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents == null)
            {
                report.AppendLine($"프리팹을 찾을 수 없습니다: {path}");
                continue;
            }

            foreach (string name in BOOSTED_NAMES)
            {
                Color? original = FindStartColor(baseline, name);
                ParticleSystem target = Find(contents, name);

                if (original == null || target == null)
                {
                    report.AppendLine($"  찾지 못함: {name}");
                    continue;
                }

                Color applied = Scaled(original.Value, multiplier);

                var main = target.main;
                main.startColor = applied;

                report.Append("  ").Append(name.PadRight(12));
                report.Append(Format(original.Value)).Append(" -> ").AppendLine(Format(applied));
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            report.AppendLine($"  저장: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    private static Color? FindStartColor(GameObject root, string name)
    {
        ParticleSystem found = Find(root, name);
        return found == null ? (Color?)null : found.main.startColor.color;
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

    // 알파는 그대로 둔다. 알파를 건드리면 페이드 아웃 타이밍이 같이 바뀐다.
    private static Color Scaled(Color source, float multiplier)
    {
        return new Color(source.r * multiplier, source.g * multiplier, source.b * multiplier, source.a);
    }

    private static string Format(Color color)
    {
        return $"({color.r:0.##}, {color.g:0.##}, {color.b:0.##}, a={color.a:0.##})";
    }
}
