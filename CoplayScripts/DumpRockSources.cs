using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpRockSources
{
    private const string FOLDER = "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in new[] { "FX_Crack_Rock", "FX_Crack_RockAOE" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{name}.prefab");
            sb.AppendLine($"== {name} rootScale={root.transform.localScale.x:0.##}");

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Vector3 bounds = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh &&
                    renderer.mesh != null ? renderer.mesh.bounds.size : Vector3.one;
                Vector3 lossy = ps.transform.lossyScale;
                float longest = Mathf.Max(bounds.x * lossy.x, Mathf.Max(bounds.y * lossy.y, bounds.z * lossy.z)) *
                    main.startSize.constant;

                sb.Append("   ").Append(ps.gameObject.name.PadRight(18));
                sb.Append(" parent=").Append((ps.transform.parent == null ? "-" : ps.transform.parent.name).PadRight(18));
                sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(20));
                sb.Append(" native=").Append(longest.ToString("0.##").PadRight(7));
                sb.Append(" life=").Append(main.startLifetime.constant.ToString("0.##").PadRight(5));
                sb.Append(" loop=").Append(main.loop ? "1" : "0");
                sb.Append(" rot=").Append(ps.transform.localEulerAngles.ToString("0").PadRight(16));
                sb.Append(" mat=").Append(renderer == null || renderer.sharedMaterial == null
                    ? "-" : renderer.sharedMaterial.name);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
