using System.Text;
using UnityEditor;
using UnityEngine;

public static class VerifyTowerEffects
{
    private static readonly string[] NAMES =
    {
        "Arrow", "CrossBow", "Musket", "AntiAir", "Fire", "Ice", "StoneMeteor"
    };

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in NAMES)
        {
            foreach (string kind in new[] { "Projectile", "Impact", "Muzzle" })
            {
                string path = $"Assets/Imported/Prefabs/Projectile/Tower/{kind}_Tower_{name}.prefab";
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (root == null)
                {
                    sb.AppendLine($"{name,-12} {kind,-10} MISSING");
                    continue;
                }

                sb.Append($"{name,-12} {kind,-10} scale={root.transform.localScale.x:0.##} ");

                foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    bool drawn = renderer != null && renderer.renderMode != ParticleSystemRenderMode.None;

                    sb.Append(ps.gameObject.name.Length > 10 ? "root" : ps.gameObject.name);
                    sb.Append(drawn ? "" : "(none)");
                    sb.Append(main.simulationSpace == ParticleSystemSimulationSpace.World ? "~W" : "~L");
                    sb.Append(":").Append(main.startSize.constant.ToString("0.##"));
                    sb.Append("#").Append(Hex(main.startColor));
                    sb.Append(" ");
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string Hex(ParticleSystem.MinMaxGradient gradient)
    {
        Color color = gradient.mode == ParticleSystemGradientMode.TwoColors ? gradient.colorMax : gradient.color;
        return ColorUtility.ToHtmlStringRGB(color);
    }
}
