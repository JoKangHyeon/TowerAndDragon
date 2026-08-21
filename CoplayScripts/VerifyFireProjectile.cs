using System.Text;
using UnityEditor;
using UnityEngine;

public static class VerifyFireProjectile
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        // 원본이 그대로인지부터 본다 - Imported/ 아래는 수정 금지다.
        const string SOURCE =
            "Assets/Imported/Vefects/Anime VFX URP/Shared/Particles/VFX_Fireball_Projectile_Static.prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE);
        sb.AppendLine($"source scale={source.transform.localScale.x:0.##} " +
                      $"ps={source.GetComponentsInChildren<ParticleSystem>(true).Length} " +
                      $"projectileScript={(source.GetComponent<Projectile>() == null ? "none" : "PRESENT")} " +
                      $"layer={source.GetComponentInChildren<Renderer>(true).sortingLayerName}");

        var clone = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_Fire.prefab");

        sb.AppendLine($"clone scale={clone.transform.localScale.x:0.###} " +
                      $"Projectile={(clone.GetComponent<Projectile>() != null)} " +
                      $"Visual={(clone.GetComponent<ProjectileVisual>() != null)}");

        foreach (ParticleSystem ps in clone.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            float effective = main.startSize.constant * clone.transform.localScale.x;

            sb.Append("  ").Append(ps.gameObject.name.PadRight(44));
            sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(9));
            sb.Append(" space=").Append(main.simulationSpace.ToString().PadRight(6));
            sb.Append(" eff=").Append(effective.ToString("0.###").PadRight(7));
            sb.Append(" layer=").Append(renderer == null ? "-" : renderer.sortingLayerName + "/" + renderer.sortingOrder);
            sb.AppendLine();
        }

        var visual = clone.GetComponent<ProjectileVisual>();
        var so = new SerializedObject(visual);
        sb.AppendLine("muzzle=" + so.FindProperty("_muzzlePrefab").objectReferenceValue);
        sb.AppendLine("impact=" + so.FindProperty("_impactPrefab").objectReferenceValue);
        sb.AppendLine("muzzleLife=" + so.FindProperty("_muzzleLifetimeSeconds").floatValue +
                      " impactLife=" + so.FindProperty("_impactLifetimeSeconds").floatValue);

        var towerData = AssetDatabase.LoadAssetAtPath<TowerData>("Assets/Data/TowerData/TD_FireTower.asset");
        sb.AppendLine("TD_FireTower -> " + AssetDatabase.GetAssetPath(towerData.ProjectilePrefab));

        return sb.ToString();
    }
}
