using System.Text;
using UnityEditor;
using UnityEngine;

public static class CheckSmokeGravity
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string path in new[]
        {
            "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_24_green_explosion.prefab",
            "Assets/Imported/Prefabs/Effects/Status/FX_Impact_StoneCrack.prefab",
            "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab"
        })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("== " + path);

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!ps.gameObject.name.ToLowerInvariant().Contains("smoke"))
                {
                    continue;
                }

                var main = ps.main;
                var velocity = ps.velocityOverLifetime;

                sb.Append("   ").Append(ps.gameObject.name.PadRight(12));
                sb.Append(" gravity=").Append(main.gravityModifier.constant.ToString("0.###").PadRight(8));
                sb.Append(" mode=").Append(main.gravityModifier.mode.ToString().PadRight(14));
                sb.Append(" startSpeed=").Append(main.startSpeed.constant.ToString("0.##").PadRight(6));
                sb.Append(" life=").Append(main.startLifetime.constant.ToString("0.##").PadRight(5));
                sb.Append(" space=").Append(main.simulationSpace.ToString().PadRight(6));
                sb.Append(" velOverLife=").Append(velocity.enabled
                    ? $"on(y={velocity.y.constant:0.##})"
                    : "off");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
