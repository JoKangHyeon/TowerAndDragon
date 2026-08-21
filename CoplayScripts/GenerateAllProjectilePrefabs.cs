using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Coplay execute_script 전용 일회성 에디터 스크립트.
// Hovl Studio 원본은 유지하고, 게임용 투사체/머즐/명중 프리팹을 별도 폴더에 만든다.
public static class GenerateAllProjectilePrefabs
{
    private const string VOL1_PROJECTILE_SOURCE =
        "Assets/Imported/Hovl Studio/AAA Projectiles Vol 1/Prefabs/Projectiles 2D";
    private const string VOL1_EFFECT_SOURCE =
        "Assets/Imported/Hovl Studio/AAA Projectiles Vol 1/Prefabs/Flash and hits";
    private const string VOL2_SOURCE =
        "Assets/Imported/Hovl Studio/AAA Projectiles Vol 2/Prefabs";
    private const string OUTPUT_ROOT = "Assets/Imported/Prefabs/Projectile";
    private const string VOL1_OUTPUT = OUTPUT_ROOT + "/AAA_Vol1";
    private const string VOL2_OUTPUT = OUTPUT_ROOT + "/AAA_Vol2";
    private const string PROJECTILE_SORTING_LAYER = "Projectile";
    private const float VOL2_PROJECTILE_SCALE = 0.33f;

    private static readonly Regex VOL1_PROJECTILE_PATTERN =
        new Regex(@"^2D Projectile (?<number>\d+) (?<label>.+)$", RegexOptions.Compiled);
    private static readonly Regex VOL2_PROJECTILE_PATTERN =
        new Regex(@"^Projectile (?<number>\d+) (?<label>.+)$", RegexOptions.Compiled);
    private static readonly Regex BUNDLED_EFFECT_PATTERN =
        new Regex(@"^(Flash|Hit) \d+$", RegexOptions.Compiled);

    public static string GenerateVol1()
    {
        return GenerateVolume(
            volume: 1,
            sourceFolder: VOL1_PROJECTILE_SOURCE,
            outputFolder: VOL1_OUTPUT,
            sourcePattern: VOL1_PROJECTILE_PATTERN);
    }

    public static string GenerateVol2()
    {
        return GenerateVolume(
            volume: 2,
            sourceFolder: VOL2_SOURCE,
            outputFolder: VOL2_OUTPUT,
            sourcePattern: VOL2_PROJECTILE_PATTERN);
    }

    public static string ValidateAll()
    {
        var report = new StringBuilder();
        int errors = 0;

        errors += ValidateVolume(VOL1_OUTPUT, 1, report);
        errors += ValidateVolume(VOL2_OUTPUT, 2, report);

        int projectileCount = CountAssets(VOL1_OUTPUT, "Projectile_V1_") +
            CountAssets(VOL2_OUTPUT, "Projectile_V2_");
        int impactCount = CountAssets(VOL1_OUTPUT, "Impact_V1_") +
            CountAssets(VOL2_OUTPUT, "Impact_V2_");
        int muzzleCount = CountAssets(VOL1_OUTPUT, "Muzzle_V1_") +
            CountAssets(VOL2_OUTPUT, "Muzzle_V2_");

        report.Insert(
            0,
            $"검증 결과: errors={errors}, projectiles={projectileCount}, " +
            $"impacts={impactCount}, muzzles={muzzleCount}\n");

        if (errors == 0)
        {
            report.AppendLine("모든 생성 프리팹의 구조와 배선이 정상입니다.");
        }

        return report.ToString();
    }

    private static string GenerateVolume(
        int volume,
        string sourceFolder,
        string outputFolder,
        Regex sourcePattern)
    {
        EnsureFolder(OUTPUT_ROOT);
        EnsureFolder(outputFolder);

        var entries = FindEntries(sourceFolder, sourcePattern);
        var report = new StringBuilder();
        int projectileCount = 0;
        int impactCount = 0;
        int muzzleCount = 0;
        int missingMuzzleCount = 0;

        try
        {
            foreach (Entry entry in entries)
            {
                string suffix = BuildSuffix(volume, entry.Number, entry.Label);
                string impactDestination = $"{outputFolder}/Impact_{suffix}.prefab";
                string muzzleDestination = $"{outputFolder}/Muzzle_{suffix}.prefab";
                string projectileDestination = $"{outputFolder}/Projectile_{suffix}.prefab";

                string impactSource = ResolveEffectSource(volume, entry.Number, entry.Label, isImpact: true);
                string muzzleSource = ResolveEffectSource(volume, entry.Number, entry.Label, isImpact: false);

                if (string.IsNullOrEmpty(impactSource))
                {
                    throw new InvalidOperationException(
                        $"Impact 원본을 찾을 수 없습니다: Vol{volume} #{entry.Number} {entry.Label}");
                }

                CopyFresh(impactSource, impactDestination);
                EditEffectPrefab(impactDestination, $"Impact_{suffix}");
                impactCount++;

                if (!string.IsNullOrEmpty(muzzleSource))
                {
                    CopyFresh(muzzleSource, muzzleDestination);
                    EditEffectPrefab(muzzleDestination, $"Muzzle_{suffix}");
                    muzzleCount++;
                }
                else
                {
                    DeleteIfExists(muzzleDestination);
                    missingMuzzleCount++;
                }

                CopyFresh(entry.SourcePath, projectileDestination);
                EditProjectilePrefab(
                    projectileDestination,
                    $"Projectile_{suffix}",
                    volume,
                    impactDestination,
                    string.IsNullOrEmpty(muzzleSource) ? null : muzzleDestination);
                projectileCount++;
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        report.AppendLine($"Vol{volume} 생성 완료");
        report.AppendLine($"- Projectile: {projectileCount}");
        report.AppendLine($"- Impact: {impactCount}");
        report.AppendLine($"- Muzzle: {muzzleCount}");
        report.AppendLine($"- 원본 Muzzle이 없어 미배선: {missingMuzzleCount}");
        report.AppendLine($"- 출력: {outputFolder}");

        return report.ToString();
    }

    private static List<Entry> FindEntries(string sourceFolder, Regex sourcePattern)
    {
        var entries = new List<Entry>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { sourceFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (!string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), sourceFolder,
                    StringComparison.Ordinal))
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(path);
            Match match = sourcePattern.Match(name);

            if (!match.Success)
            {
                continue;
            }

            entries.Add(new Entry
            {
                Number = int.Parse(match.Groups["number"].Value),
                Label = match.Groups["label"].Value,
                SourcePath = path
            });
        }

        return entries.OrderBy(entry => entry.Number).ToList();
    }

    private static string ResolveEffectSource(int volume, int number, string label, bool isImpact)
    {
        string prefix = isImpact ? "Hit" : "Flash";
        string path = volume == 1
            ? $"{VOL1_EFFECT_SOURCE}/{prefix} {number} {label}.prefab"
            : $"{VOL2_SOURCE}/{prefix} {number}.prefab";

        return AssetDatabase.LoadAssetAtPath<GameObject>(path) != null ? path : null;
    }

    private static void EditEffectPrefab(string prefabPath, string rootName)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            root.name = rootName;
            ResetRootTransform(root.transform, Vector3.one);
            RemoveComponentsByName(root, "HS_CallBackParent", "HS_ProjectileMover", "HS_ProjectileMover2D");
            RemovePhysicsAndLights(root);
            PrepareParticles(root);
            ApplySorting(root);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EditProjectilePrefab(
        string prefabPath,
        string rootName,
        int volume,
        string impactPath,
        string muzzlePath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

        try
        {
            root.name = rootName;
            Vector3 scale = volume == 2
                ? Vector3.one * VOL2_PROJECTILE_SCALE
                : Vector3.one;
            ResetRootTransform(root.transform, scale);

            RemoveComponentsByName(root, "HS_ProjectileMover", "HS_ProjectileMover2D");
            RemovePhysicsAndLights(root);

            if (volume == 2)
            {
                RemoveBundledEffects(root);
            }

            PrepareParticles(root);
            ApplySorting(root);

            Projectile projectile = root.GetComponent<Projectile>();
            if (projectile == null)
            {
                projectile = root.AddComponent<Projectile>();
            }

            ProjectileVisual visual = root.GetComponent<ProjectileVisual>();
            if (visual == null)
            {
                visual = root.AddComponent<ProjectileVisual>();
            }

            var serializedVisual = new SerializedObject(visual);
            serializedVisual.FindProperty("_trailParticles").arraySize = 0;
            serializedVisual.FindProperty("_muzzlePrefab").objectReferenceValue =
                string.IsNullOrEmpty(muzzlePath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<GameObject>(muzzlePath);
            serializedVisual.FindProperty("_impactPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(impactPath);
            serializedVisual.FindProperty("_rotateImpactToTravelDirection").boolValue = true;
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveBundledEffects(GameObject root)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in transforms)
        {
            if (child == null || child == root.transform)
            {
                continue;
            }

            if (BUNDLED_EFFECT_PATTERN.IsMatch(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void RemovePhysicsAndLights(GameObject root)
    {
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().Name;
            if (typeName == "UniversalAdditionalLightData")
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
        {
            UnityEngine.Object.DestroyImmediate(collider);
        }

        foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
        {
            UnityEngine.Object.DestroyImmediate(body);
        }

        foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>(true))
        {
            UnityEngine.Object.DestroyImmediate(body);
        }

        foreach (Light light in root.GetComponentsInChildren<Light>(true))
        {
            UnityEngine.Object.DestroyImmediate(light);
        }
    }

    private static void RemoveComponentsByName(GameObject root, params string[] names)
    {
        var targets = new HashSet<string>(names, StringComparer.Ordinal);

        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component != null && targets.Contains(component.GetType().Name))
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }
    }

    private static void PrepareParticles(GameObject root)
    {
        foreach (ParticleSystem particle in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particle.main;
            main.stopAction = ParticleSystemStopAction.None;
        }
    }

    private static void ApplySorting(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerName = PROJECTILE_SORTING_LAYER;
        }
    }

    private static void ResetRootTransform(Transform root, Vector3 scale)
    {
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = scale;
    }

    private static int ValidateVolume(string folder, int volume, StringBuilder report)
    {
        int errors = 0;
        string prefix = $"Projectile_V{volume}_";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
        var paths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToList();
        var projectilePaths = paths
            .Where(path => Path.GetFileNameWithoutExtension(path).StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(path => path)
            .ToList();
        var impactPaths = paths
            .Where(path => Path.GetFileNameWithoutExtension(path)
                .StartsWith($"Impact_V{volume}_", StringComparison.Ordinal))
            .OrderBy(path => path)
            .ToList();
        var muzzlePaths = paths
            .Where(path => Path.GetFileNameWithoutExtension(path)
                .StartsWith($"Muzzle_V{volume}_", StringComparison.Ordinal))
            .OrderBy(path => path)
            .ToList();

        int expectedCount = volume == 1 ? 27 : 25;
        if (projectilePaths.Count != expectedCount)
        {
            report.AppendLine($"ERROR Vol{volume}: Projectile 수 {projectilePaths.Count}/{expectedCount}");
            errors++;
        }

        if (impactPaths.Count != expectedCount)
        {
            report.AppendLine($"ERROR Vol{volume}: Impact 수 {impactPaths.Count}/{expectedCount}");
            errors++;
        }

        int expectedMuzzleCount = expectedCount - 3;
        if (muzzlePaths.Count != expectedMuzzleCount)
        {
            report.AppendLine($"ERROR Vol{volume}: Muzzle 수 {muzzlePaths.Count}/{expectedMuzzleCount}");
            errors++;
        }

        foreach (string path in projectilePaths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                errors += ValidateProjectile(root, path, report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        foreach (string path in impactPaths.Concat(muzzlePaths))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                errors += ValidateEffect(root, path, report);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return errors;
    }

    private static int ValidateEffect(GameObject root, string path, StringBuilder report)
    {
        int errors = 0;

        if (HasComponentNamed(root, "HS_CallBackParent") ||
            HasComponentNamed(root, "HS_ProjectileMover") ||
            HasComponentNamed(root, "HS_ProjectileMover2D") ||
            root.GetComponentsInChildren<Rigidbody>(true).Length > 0 ||
            root.GetComponentsInChildren<Rigidbody2D>(true).Length > 0 ||
            root.GetComponentsInChildren<Collider>(true).Length > 0 ||
            root.GetComponentsInChildren<Collider2D>(true).Length > 0 ||
            root.GetComponentsInChildren<Light>(true).Length > 0)
        {
            report.AppendLine($"ERROR {path}: 이펙트 제거 대상 컴포넌트 잔존");
            errors++;
        }

        if (root.GetComponentsInChildren<ParticleSystem>(true).Any(
                particle => particle.main.stopAction == ParticleSystemStopAction.Callback))
        {
            report.AppendLine($"ERROR {path}: 이펙트 ParticleSystem Callback 잔존");
            errors++;
        }

        int sortingLayerId = SortingLayer.NameToID(PROJECTILE_SORTING_LAYER);
        if (root.GetComponentsInChildren<Renderer>(true).Any(
                renderer => renderer.sortingLayerID != sortingLayerId))
        {
            report.AppendLine($"ERROR {path}: 이펙트 Projectile Sorting Layer 누락");
            errors++;
        }

        return errors;
    }

    private static int ValidateProjectile(GameObject root, string path, StringBuilder report)
    {
        int errors = 0;
        Projectile projectile = root.GetComponent<Projectile>();
        ProjectileVisual visual = root.GetComponent<ProjectileVisual>();

        if (projectile == null || visual == null)
        {
            report.AppendLine($"ERROR {path}: Projectile/ProjectileVisual 누락");
            errors++;
        }

        if (root.GetComponentsInChildren<Rigidbody>(true).Length > 0 ||
            root.GetComponentsInChildren<Rigidbody2D>(true).Length > 0 ||
            root.GetComponentsInChildren<Collider>(true).Length > 0 ||
            root.GetComponentsInChildren<Collider2D>(true).Length > 0 ||
            root.GetComponentsInChildren<Light>(true).Length > 0 ||
            HasComponentNamed(root, "HS_ProjectileMover") ||
            HasComponentNamed(root, "HS_ProjectileMover2D"))
        {
            report.AppendLine($"ERROR {path}: 제거 대상 컴포넌트 잔존");
            errors++;
        }

        int sortingLayerId = SortingLayer.NameToID(PROJECTILE_SORTING_LAYER);
        if (root.GetComponentsInChildren<Renderer>(true).Any(
                renderer => renderer.sortingLayerID != sortingLayerId))
        {
            report.AppendLine($"ERROR {path}: Projectile Sorting Layer 누락");
            errors++;
        }

        if (root.GetComponentsInChildren<ParticleSystem>(true).Any(
                particle => particle.main.stopAction == ParticleSystemStopAction.Callback))
        {
            report.AppendLine($"ERROR {path}: ParticleSystem Callback 잔존");
            errors++;
        }

        if (visual != null)
        {
            var serializedVisual = new SerializedObject(visual);
            if (serializedVisual.FindProperty("_impactPrefab").objectReferenceValue == null)
            {
                report.AppendLine($"ERROR {path}: Impact 미배선");
                errors++;
            }
        }

        return errors;
    }

    private static bool HasComponentNamed(GameObject root, string typeName)
    {
        return root.GetComponentsInChildren<Component>(true).Any(
            component => component != null && component.GetType().Name == typeName);
    }

    private static int CountAssets(string folder, string prefix)
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Count(path => Path.GetFileNameWithoutExtension(path)
                .StartsWith(prefix, StringComparison.Ordinal));
    }

    private static string BuildSuffix(int volume, int number, string label)
    {
        string normalizedLabel = Regex.Replace(label, @"[^A-Za-z0-9]+", "_").Trim('_');
        return $"V{volume}_{number:00}_{normalizedLabel}";
    }

    private static void CopyFresh(string sourcePath, string destinationPath)
    {
        DeleteIfExists(destinationPath);

        if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
        {
            throw new InvalidOperationException(
                $"프리팹 복제 실패: {sourcePath} -> {destinationPath}");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string name = Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, name);
    }

    private sealed class Entry
    {
        public int Number;
        public string Label;
        public string SourcePath;
    }
}
