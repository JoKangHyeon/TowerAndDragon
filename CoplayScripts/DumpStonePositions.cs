using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpStonePositions
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string path in new[]
        {
            "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Crack_Rock.prefab",
            "Assets/Imported/Prefabs/Effects/Status/FX_Impact_StoneCrack.prefab"
        })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("== " + root.name);

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                string n = ps.gameObject.name;

                if (!n.StartsWith("rock") && n != "Mountain" && !n.StartsWith("stone"))
                {
                    continue;
                }

                var shape = ps.shape;
                var main = ps.main;

                sb.Append("   ").Append(n.PadRight(12));
                sb.Append(" localPos=").Append(ps.transform.localPosition.ToString("0.###").PadRight(24));
                sb.Append(" worldPos=").Append(ps.transform.position.ToString("0.###").PadRight(24));
                sb.Append(" shape=").Append((shape.enabled ? shape.shapeType.ToString() : "off").PadRight(10));
                sb.Append(" shapePos=").Append((shape.enabled ? shape.position.ToString("0.##") : "-").PadRight(20));
                sb.Append(" gravity=").Append(main.gravityModifier.constant.ToString("0.##"));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}
