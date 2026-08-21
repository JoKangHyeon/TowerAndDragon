using System.Text;
using UnityEditor;
using UnityEngine;

public static class VerifyRotateFlags
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in new[] { "Arrow", "CrossBow", "Musket", "AntiAir", "Fire", "Ice", "StoneMeteor" })
        {
            var visual = AssetDatabase
                .LoadAssetAtPath<GameObject>($"Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_{name}.prefab")
                .GetComponent<ProjectileVisual>();

            var so = new SerializedObject(visual);
            sb.Append(name.PadRight(14));
            sb.Append(" rotateImpact=").Append(so.FindProperty("_rotateImpactToTravelDirection").boolValue);
            sb.Append(" impactLife=").Append(so.FindProperty("_impactLifetimeSeconds").floatValue);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
