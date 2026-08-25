using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>새끼용 범위 프리팹의 하위 파티클별 실제 렌더 경계를 측정한다.</summary>
public static class MeasureBabyDragonRangeParticles
{
    private const string REPORT_PATH =
        "output/vfx_check/baby_dragon_range_particle_bounds.txt";
    private const float SAMPLE_TIME = 30.5f;

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");
        var report = new StringBuilder();

        Measure(
            "Assets/Imported/Prefabs/BabyDragon/Range/FX_BD_AttackRange_Ice.prefab",
            report);
        Measure(
            "Assets/Imported/Prefabs/BabyDragon/Range/FX_BD_BuffRange_Ice.prefab",
            report);
        Measure(
            "Assets/Imported/Hovl Studio_waypoints/Way points/Prefabs/" +
            "Marker circle swords cyan.prefab",
            report);

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static void Measure(string path, StringBuilder report)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        GameObject instance = Object.Instantiate(prefab);
        instance.transform.position = Vector3.zero;
        instance.transform.localScale = Vector3.one;
        report.AppendLine(prefab.name);

        ParticleSystem[] systems =
            instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem system in systems)
        {
            system.useAutoRandomSeed = false;
            system.randomSeed = 24680;
            system.Clear(false);
            system.Simulate(SAMPLE_TIME, false, true);

            ParticleSystemRenderer renderer =
                system.GetComponent<ParticleSystemRenderer>();
            Bounds bounds = renderer != null ? renderer.bounds : default;
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.EmissionModule emission = system.emission;
            ParticleSystem.VelocityOverLifetimeModule velocity =
                system.velocityOverLifetime;
            ParticleSystem.SizeOverLifetimeModule size =
                system.sizeOverLifetime;
            ParticleSystem.ColorOverLifetimeModule color =
                system.colorOverLifetime;
            ParticleSystem.RotationOverLifetimeModule rotation =
                system.rotationOverLifetime;
            ParticleSystem.TextureSheetAnimationModule texture =
                system.textureSheetAnimation;
            report.AppendLine(
                $"  {PathOf(instance.transform, system.transform)} " +
                $"material={renderer?.sharedMaterial?.name} " +
                $"count={system.particleCount} " +
                $"center=({bounds.center.x:F3},{bounds.center.y:F3}) " +
                $"ext=({bounds.extents.x:F3},{bounds.extents.y:F3}) " +
                $"duration={main.duration:F3} life={main.startLifetime.constantMax:F3} " +
                $"rate={emission.rateOverTime.constantMax:F3} bursts={emission.burstCount} " +
                $"velocity={velocity.enabled} vy={velocity.y.constantMax:F3} " +
                $"sizeLife={size.enabled} size=" +
                $"({Evaluate(size.size, 0f):F2},{Evaluate(size.size, 0.25f):F2}," +
                $"{Evaluate(size.size, 0.5f):F2},{Evaluate(size.size, 0.75f):F2}," +
                $"{Evaluate(size.size, 1f):F2}) colorLife={color.enabled} " +
                $"startRotZ=({main.startRotation.constantMin:F3}," +
                $"{main.startRotation.constantMax:F3}) " +
                $"rotationLife={rotation.enabled} " +
                $"rotZ=({rotation.z.constantMin:F3},{rotation.z.constantMax:F3}) " +
                $"texture={texture.enabled}");
        }

        report.AppendLine();
        Object.DestroyImmediate(instance);
    }

    private static float Evaluate(
        ParticleSystem.MinMaxCurve curve,
        float time)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Curve:
                return curve.curve.Evaluate(time) * curve.curveMultiplier;
            case ParticleSystemCurveMode.TwoCurves:
                return Mathf.Max(
                    curve.curveMin.Evaluate(time),
                    curve.curveMax.Evaluate(time)) * curve.curveMultiplier;
            case ParticleSystemCurveMode.TwoConstants:
                return curve.constantMax;
            default:
                return curve.constant;
        }
    }

    private static string PathOf(Transform root, Transform child)
    {
        string path = child.name;
        for (Transform current = child.parent;
             current != null && current != root;
             current = current.parent)
        {
            path = current.name + "/" + path;
        }

        return path;
    }
}
