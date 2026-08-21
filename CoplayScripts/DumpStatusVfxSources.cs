using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpStatusVfxSources
{
    private const string FOLDER = "Assets/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in new[] { "FX_GroundCrack_Blue", "FX_Dust_Fire" })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>($"{FOLDER}/{name}.prefab");
            sb.AppendLine($"== {name} rootScale={root.transform.localScale.x:0.##}");

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Transform t = ps.transform;

                sb.Append("   ").Append(ps.gameObject.name.PadRight(20));
                sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(20));
                sb.Append(" rot=").Append(t.localEulerAngles.ToString("0").PadRight(16));
                sb.Append(" size=").Append(main.startSize.constant.ToString("0.##").PadRight(6));
                sb.Append(" space=").Append(main.simulationSpace.ToString().PadRight(6));
                sb.Append(" loop=").Append(main.loop ? "1" : "0");
                sb.Append(" rateTime=").Append(ps.emission.rateOverTime.constant.ToString("0.#"));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
