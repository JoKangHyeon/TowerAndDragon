using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>각 속성 검의 실제 반복 주기를 공격·버프 범위 링 밝기 주기에 적용한다.</summary>
public static class SyncBabyDragonRangePulse
{
    private const string RANGE_ROOT =
        "Assets/Imported/Prefabs/BabyDragon/Range/";
    private const string MARKER_CHILD = "Marker";
    private const string CIRCLE_CHILD = "Circle";
    private const string REPORT_PATH =
        "output/vfx_check/baby_dragon_pulse_sync.txt";

    private static readonly string[] KEYS =
    {
        "Ice", "Fire", "Time", "Stone", "Life",
    };

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");
        var report = new StringBuilder();

        foreach (string key in KEYS)
        {
            string attackPath =
                RANGE_ROOT + "FX_BD_AttackRange_" + key + ".prefab";
            float swordPeriod = ReadSwordPeriod(attackPath);

            ApplyRingPeriod(attackPath, swordPeriod);
            ApplyRingPeriod(
                RANGE_ROOT + "FX_BD_BuffRange_" + key + ".prefab",
                swordPeriod);

            report.AppendLine(
                $"{key}: sword/attack/buff = {swordPeriod:F3}s");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static float ReadSwordPeriod(string prefabPath)
    {
        GameObject prefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        ParticleSystem sword = prefab != null
            ? prefab.transform.Find(MARKER_CHILD)?.GetComponent<ParticleSystem>()
            : null;
        return sword != null ? sword.main.duration : 1f;
    }

    private static void ApplyRingPeriod(
        string prefabPath,
        float periodSeconds)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        ParticleSystem circle = FindCircle(root.transform);

        if (circle != null)
        {
            ParticleSystem.MainModule main = circle.main;
            main.duration = periodSeconds;
            main.startLifetime = periodSeconds;
        }

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static ParticleSystem FindCircle(Transform root)
    {
        foreach (ParticleSystem particles in
            root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.name == CIRCLE_CHILD)
            {
                return particles;
            }
        }

        return null;
    }
}
