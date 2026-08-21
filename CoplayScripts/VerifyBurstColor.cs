using System.Text;
using UnityEditor;
using UnityEngine;

public static class VerifyBurstColor
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string path in new[]
        {
            "Assets/Imported/Prefabs/Effects/Status/FX_Impact_StoneCrack.prefab",
            "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab"
        })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("== " + root.name);

            bool inBurst = false;

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                string n = ps.gameObject.name;
                inBurst |= n == "Burst";

                if (!inBurst)
                {
                    continue;
                }

                var gradient = ps.main.startColor;
                Color color = gradient.mode == ParticleSystemGradientMode.TwoColors
                    ? gradient.colorMax
                    : gradient.color;

                sb.Append("   ").Append(n.PadRight(12));
                sb.Append(" #").Append(ColorUtility.ToHtmlStringRGB(color));
                sb.Append("  mat=").Append(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.name);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
