using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AuditImpacts
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in new[] { "Arrow", "CrossBow", "Musket", "AntiAir", "Fire", "Ice", "StoneMeteor" })
        {
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_{name}.prefab");

            if (impact == null)
            {
                continue;
            }

            var rows = new List<(string label, float size, float endsAt)>();
            var names = new List<string>();
            float longestLife = 0f;

            foreach (ParticleSystem ps in impact.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                names.Add(ps.gameObject.name);

                if (renderer == null || renderer.renderMode == ParticleSystemRenderMode.None)
                {
                    continue;
                }

                Vector3 bounds = renderer.renderMode == ParticleSystemRenderMode.Mesh && renderer.mesh != null
                    ? renderer.mesh.bounds.size
                    : Vector3.one;

                Vector3 lossy = ps.transform.lossyScale;
                float size = Mathf.Max(bounds.x * lossy.x, Mathf.Max(bounds.y * lossy.y, bounds.z * lossy.z)) *
                    main.startSize.constant;

                float endsAt = main.duration + main.startLifetime.constant;

                if (!main.loop)
                {
                    longestLife = Mathf.Max(longestLife, main.startLifetime.constant);
                }

                rows.Add((ps.gameObject.name, size, endsAt));
            }

            var visual = AssetDatabase
                .LoadAssetAtPath<GameObject>($"Assets/Imported/Prefabs/Projectile/Tower/Projectile_Tower_{name}.prefab")
                .GetComponent<ProjectileVisual>();
            var so = new SerializedObject(visual);
            float configuredLife = so.FindProperty("_impactLifetimeSeconds").floatValue;

            rows.Sort((a, b) => b.size.CompareTo(a.size));

            sb.AppendLine($"== {name}  systems={rows.Count}  설정수명={configuredLife}  최장입자수명={longestLife:0.##}");
            sb.Append("   최대 5개: ");
            foreach ((string label, float size, float _) in rows.Take(5))
            {
                sb.Append($"{label}={size:0.##}  ");
            }
            sb.AppendLine();

            if (longestLife > configuredLife + 0.05f)
            {
                sb.AppendLine($"   [잘림] 입자 수명 {longestLife:0.##}초 > 설정 {configuredLife}초");
            }

            var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
            {
                sb.AppendLine("   [이름중복] " + string.Join(", ", duplicates));
            }
        }

        return sb.ToString();
    }
}
