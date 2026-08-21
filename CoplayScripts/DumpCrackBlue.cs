using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpCrackBlue
{
    private const string ROOT = "Assets/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs";

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string relative in new[]
        {
            "Built-In/FX_Crack_Blue", "URP/FX_Crack_Blue02", "Built-In/FX_Crack_Blue02"
        })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>($"{ROOT}/{relative}.prefab");

            if (root == null)
            {
                sb.AppendLine($"== {relative} MISSING");
                continue;
            }

            sb.AppendLine($"== {relative}  rootScale={root.transform.localScale.x:0.##}");

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Material material = renderer == null ? null : renderer.sharedMaterial;

                string parent = ps.transform.parent == null ? "-" : ps.transform.parent.name;

                sb.Append("   ").Append(ps.gameObject.name.PadRight(18));
                sb.Append(" parent=").Append(parent.PadRight(18));
                sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(20));
                sb.Append(" size=").Append(main.startSize.constant.ToString("0.##").PadRight(6));
                sb.Append(" loop=").Append(main.loop ? "1" : "0");
                sb.Append(" rateT=").Append(ps.emission.rateOverTime.constant.ToString("0.#").PadRight(6));
                sb.Append(" rot=").Append(ps.transform.localEulerAngles.ToString("0").PadRight(16));
                sb.Append(" mat=").Append(material == null ? "<none>" : material.name.PadRight(18));

                if (material != null && material.shader != null)
                {
                    sb.Append(" shader=").Append(material.shader.name.PadRight(28));
                    sb.Append(" err=").Append(ShaderUtil.ShaderHasError(material.shader));
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
