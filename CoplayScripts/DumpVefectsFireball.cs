using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpVefectsFireball
{
    private const string PATH =
        "Assets/Imported/Vefects/Anime VFX URP/Shared/Particles/VFX_Fireball_Projectile_Static.prefab";

    public static string Execute()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);

        if (root == null)
        {
            return "MISSING";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"root scale={root.transform.localScale} children={root.transform.childCount}");

        var shaders = new HashSet<string>();
        var layers = new HashSet<string>();

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            sb.Append("  ").Append(ps.gameObject.name.PadRight(42));
            sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(9));
            sb.Append(" space=").Append(main.simulationSpace.ToString().PadRight(6));
            sb.Append(" size=").Append(main.startSize.constant.ToString("0.##").PadRight(6));
            sb.Append(" life=").Append(main.startLifetime.constant.ToString("0.##").PadRight(5));
            sb.Append(" loop=").Append(main.loop ? "1" : "0");
            sb.Append(" rateDist=").Append(ps.emission.rateOverDistance.constant.ToString("0.#").PadRight(5));

            if (renderer != null)
            {
                if (renderer != null && renderer.renderMode == ParticleSystemRenderMode.Stretch)
            {
                sb.Append(" len=").Append(renderer.lengthScale.ToString("0.##"));
                sb.Append(" vel=").Append(renderer.velocityScale.ToString("0.###"));
                sb.Append(" spd=").Append(main.startSpeed.constant.ToString("0.##"));
            }

            sb.Append(" layer=").Append(renderer.sortingLayerName).Append("/").Append(renderer.sortingOrder);
                layers.Add(renderer.sortingLayerName + "/" + renderer.sortingOrder);

                if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null)
                {
                    shaders.Add(renderer.sharedMaterial.shader.name);
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("shaders: " + string.Join(" | ", shaders));
        sb.AppendLine("sortingLayers: " + string.Join(" | ", layers));
        sb.AppendLine("components: " + string.Join(" ",
            System.Linq.Enumerable.Select(root.GetComponentsInChildren<Component>(true), c => c.GetType().Name)
                is var names ? new HashSet<string>(names) : null));

        return sb.ToString();
    }
}
