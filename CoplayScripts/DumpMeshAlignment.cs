using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpMeshAlignment
{
    private static readonly string[] SOURCES =
    {
        "AAA_Vol1/Projectile_V1_20_pink_arrow",
        "AAA_Vol1/Projectile_V1_21_red_arrow",
        "AAA_Vol1/Projectile_V1_04_yellow_arrow",
        "AAA_Vol1/Projectile_V1_11_orange_arrow"
    };

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in SOURCES)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Imported/Prefabs/Projectile/{name}.prefab");

            if (root == null)
            {
                continue;
            }

            sb.AppendLine($"== {name} rootRot={root.transform.localEulerAngles}");

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();

                if (renderer == null || renderer.renderMode == ParticleSystemRenderMode.None)
                {
                    continue;
                }

                var main = ps.main;
                var rotation = ps.rotationOverLifetime;

                sb.Append("   ").Append(ps.gameObject.name.PadRight(22));
                sb.Append(" mode=").Append(renderer.renderMode.ToString().PadRight(9));
                sb.Append(" align=").Append(renderer.alignment.ToString().PadRight(8));
                sb.Append(" space=").Append(main.simulationSpace.ToString().PadRight(6));
                sb.Append(" startRotZ=").Append((main.startRotation.constant * Mathf.Rad2Deg).ToString("0.#").PadRight(7));
                sb.Append(" rotOverLife=").Append(rotation.enabled ? "1" : "0");
                sb.Append(" mesh=").Append(renderer.mesh == null ? "-" : renderer.mesh.name);
                sb.Append(" mat=").Append(renderer.sharedMaterial == null ? "-" : renderer.sharedMaterial.name);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
