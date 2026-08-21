using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpEricVfx
{
    private const string FOLDER = "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    private static readonly string[] NAMES =
    {
        "FX_Crack_Blue02", "FX_Crack_Bluerock", "FX_Crack_BlueShock", "FX_GroundCrack_Blue", "FX_Dust_Fire"
    };

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in NAMES)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{name}.prefab");

            if (root == null)
            {
                sb.AppendLine($"== {name} MISSING");
                continue;
            }

            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            float longest = 0f;
            bool anyLoop = false;
            var shaders = new HashSet<string>();
            var layers = new HashSet<string>();
            var rotations = new HashSet<string>();

            foreach (ParticleSystem ps in systems)
            {
                var main = ps.main;
                longest = Mathf.Max(longest, main.duration + main.startLifetime.constant);
                anyLoop |= main.loop;
                rotations.Add(ps.transform.localEulerAngles.ToString("0"));

                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    layers.Add(renderer.sortingLayerName + "/" + renderer.sortingOrder);
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null)
                    {
                        shaders.Add(renderer.sharedMaterial.shader.name);
                    }
                }
            }

            sb.AppendLine($"== {name}");
            sb.AppendLine($"   rootScale={root.transform.localScale.x:0.##} rootRot={root.transform.localEulerAngles:0} " +
                          $"ps={systems.Length} loop={anyLoop} longestSeconds={longest:0.##}");
            sb.AppendLine($"   childRots={string.Join(" ", rotations)}");
            sb.AppendLine($"   layers={string.Join(" ", layers)}");
            sb.AppendLine($"   shaders={string.Join(" | ", shaders)}");
        }

        return sb.ToString();
    }
}
