using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 색을 바꿀 수 있는지 판정하기 위한 계측기.
// 파티클 startColor로 틴트가 먹으려면 원본 텍스처가 무채색에 가까워야 한다 - 초록 텍스처에
// 파랑을 곱하면 파랑이 아니라 검정이 된다. 그래서 머티리얼 색과 파티클 색을 함께 본다.
public static class DumpEffectColors
{
    private static readonly string[] PATHS =
    {
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Projectile_V1_20_pink_arrow.prefab",
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_01_nature_arrow.prefab",
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_01_nature_arrow.prefab",
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_25_orange_explosion.prefab",
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_17_nova_violet.prefab",
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Projectile_V1_19_circle_bomb.prefab",
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_19_circle_bomb.prefab",
        "Assets/Imported/Prefabs/Projectile/Impact_Fire_V1.prefab",
        "Assets/Imported/Prefabs/Projectile/Muzzle_Fire_V1.prefab",
        "Assets/Imported/Prefabs/Projectile/AAA_Vol2/Impact/Impact_V2_22_star_sky.prefab",
        "Assets/Imported/Prefabs/Projectile/Projectile_Fire_V1.prefab"
    };

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string path in PATHS)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("== " + System.IO.Path.GetFileNameWithoutExtension(path));

            if (root == null)
            {
                sb.AppendLine("   MISSING");
                continue;
            }

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Material material = renderer == null ? null : renderer.sharedMaterial;

                sb.Append("   ").Append(ps.gameObject.name.PadRight(20));
                sb.Append(" mode=").Append(renderer == null ? "-" : renderer.renderMode.ToString().PadRight(9));
                sb.Append(" space=").Append(main.simulationSpace.ToString().PadRight(6));
                sb.Append(" size=").Append(main.startSize.constant.ToString("0.##").PadRight(6));
                sb.Append(" startColor=").Append(Describe(main.startColor));
                sb.Append(" mat=").Append(material == null ? "<none>" : material.name);

                if (material != null)
                {
                    var props = new List<string>();

                    foreach (string name in new[] { "_TintColor", "_Color", "_BaseColor", "_EmissionColor" })
                    {
                        if (material.HasProperty(name))
                        {
                            props.Add(name + ColorUtility.ToHtmlStringRGBA(material.GetColor(name)));
                        }
                    }

                    sb.Append(" [").Append(string.Join(" ", props)).Append("]");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string Describe(ParticleSystem.MinMaxGradient gradient)
    {
        return gradient.mode switch
        {
            ParticleSystemGradientMode.Color => "#" + ColorUtility.ToHtmlStringRGBA(gradient.color),
            ParticleSystemGradientMode.TwoColors =>
                "#" + ColorUtility.ToHtmlStringRGBA(gradient.colorMin) + "~#" +
                ColorUtility.ToHtmlStringRGBA(gradient.colorMax),
            _ => gradient.mode.ToString()
        };
    }
}
