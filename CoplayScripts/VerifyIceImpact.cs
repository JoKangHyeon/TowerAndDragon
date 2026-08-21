using System.Text;
using UnityEditor;
using UnityEngine;

public static class VerifyIceImpact
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var impact = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab");

        sb.AppendLine($"Impact_Tower_Ice rootScale={impact.transform.localScale.x:0.###} " +
                      $"ps={impact.GetComponentsInChildren<ParticleSystem>(true).Length}");

        foreach (ParticleSystem ps in impact.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            Vector3 bounds = renderer != null && renderer.renderMode == ParticleSystemRenderMode.Mesh &&
                renderer.mesh != null ? renderer.mesh.bounds.size : Vector3.one;
            Vector3 lossy = ps.transform.lossyScale;
            float longest = Mathf.Max(bounds.x * lossy.x, Mathf.Max(bounds.y * lossy.y, bounds.z * lossy.z)) *
                ps.main.startSize.constant;

            sb.Append("   ").Append(ps.gameObject.name.PadRight(20));
            sb.Append(" eff=").Append(longest.ToString("0.###").PadRight(7));
            sb.Append(" rot=").Append(ps.transform.localEulerAngles.ToString("0").PadRight(16));
            sb.Append(" layer=").Append(renderer == null ? "-" : renderer.sortingLayerName);
            sb.AppendLine();
        }

        var visual = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_Ice.prefab").GetComponent<ProjectileVisual>();
        var so = new SerializedObject(visual);
        sb.AppendLine("impact=" + so.FindProperty("_impactPrefab").objectReferenceValue +
                      " life=" + so.FindProperty("_impactLifetimeSeconds").floatValue);

        var tower = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Data/TowerData/TD_IceTower.asset");
        sb.AppendLine("TD_IceTower speed=" + tower.ProjectileSpeed +
                      " prefab=" + AssetDatabase.GetAssetPath(tower.ProjectilePrefab));

        return sb.ToString();
    }
}
