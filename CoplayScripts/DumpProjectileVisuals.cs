using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpProjectileVisuals
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
            string path = $"Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_{name}.prefab";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (root == null)
            {
                sb.AppendLine($"{name}: MISSING");
                continue;
            }

            sb.AppendLine($"== {name}  rootScale={root.transform.localScale.x:0.##}");

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var emission = ps.emission;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();

                sb.Append("   ").Append(ps.gameObject.name.PadRight(18));
                sb.Append(" mode=").Append(renderer == null ? "-" : renderer.renderMode.ToString());
                sb.Append(" size=").Append(main.startSize.constant.ToString("0.###"));
                sb.Append(" speed=").Append(main.startSpeed.constant.ToString("0.###"));
                sb.Append(" life=").Append(main.startLifetime.constant.ToString("0.##"));
                sb.Append(" dur=").Append(main.duration.ToString("0.##"));
                sb.Append(" loop=").Append(main.loop ? "1" : "0");
                sb.Append(" space=").Append(main.simulationSpace.ToString());
                sb.Append(" rateTime=").Append(emission.rateOverTime.constant.ToString("0.#"));
                sb.Append(" rateDist=").Append(emission.rateOverDistance.constant.ToString("0.#"));

                if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Stretch)
                {
                    sb.Append(" lenScale=").Append(renderer.lengthScale.ToString("0.##"));
                    sb.Append(" velScale=").Append(renderer.velocityScale.ToString("0.###"));
                }

                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
