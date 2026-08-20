using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원본 이펙트 카탈로그는 보존하면서, 타워 전용으로 과한 방사형 파티클을 덜어낸 변형을 만든다.
public static class AssignTowerProjectiles
{
    private const string OUTPUT_FOLDER = "Assets/Prefabs/Projectile/Tower";
    private const string PROJECTILE_PROPERTY = "_projectilePrefab";
    private const string MUZZLE_PROPERTY = "_muzzlePrefab";
    private const string IMPACT_PROPERTY = "_impactPrefab";
    private const string MUZZLE_LIFETIME_PROPERTY = "_muzzleLifetimeSeconds";
    private const string IMPACT_LIFETIME_PROPERTY = "_impactLifetimeSeconds";

    private sealed class TowerEffectDefinition
    {
        public string TowerDataPath;
        public string OutputName;
        public string ProjectileSourcePath;
        public string ImpactSourcePath;
        public string MuzzleSourcePath;
        public string[] RemovedImpactChildren = Array.Empty<string>();
        public string[] RemovedMuzzleChildren = Array.Empty<string>();
        public float ProjectileScaleMultiplier = 1f;
        public float ImpactScaleMultiplier = 1f;
        public float MuzzleScaleMultiplier = 1f;
        public float MuzzleLifetimeSeconds = 0.2f;
        public float ImpactLifetimeSeconds = 0.35f;
    }

    private static readonly TowerEffectDefinition[] DEFINITIONS =
    {
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_Arrow.asset",
            OutputName = "Arrow",
            ProjectileSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Projectile_V1_11_orange_arrow.prefab",
            ImpactSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Impact_V1_08_dagger.prefab",
            MuzzleSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_11_orange_arrow.prefab",
            ImpactScaleMultiplier = 0.75f,
            ImpactLifetimeSeconds = 0.22f
        },
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_CrossBow.asset",
            OutputName = "CrossBow",
            ProjectileSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Projectile_V1_01_nature_arrow.prefab",
            ImpactSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Impact_V1_01_nature_arrow.prefab",
            MuzzleSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_01_nature_arrow.prefab",
            RemovedImpactChildren = new[] { "SparksExplosion" },
            ImpactScaleMultiplier = 0.6f,
            ImpactLifetimeSeconds = 0.3f
        },
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_Musket.asset",
            OutputName = "Musket",
            ProjectileSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Projectile_V1_23_cube.prefab",
            ImpactSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Impact_V1_13_red_laser.prefab",
            MuzzleSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_23_cube.prefab",
            RemovedImpactChildren = new[] { "MuzzleFlash" },
            ProjectileScaleMultiplier = 0.45f,
            ImpactScaleMultiplier = 0.7f,
            MuzzleLifetimeSeconds = 0.18f,
            ImpactLifetimeSeconds = 0.25f
        },
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_Anti_Air.asset",
            OutputName = "AntiAir",
            ProjectileSourcePath = "Assets/Prefabs/Projectile/AAA_Vol2/Projectile_V2_25_yellow.prefab",
            ImpactSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Impact_V1_08_dagger.prefab",
            MuzzleSourcePath = "Assets/Prefabs/Projectile/AAA_Vol2/Muzzle_V2_25_yellow.prefab",
            ImpactScaleMultiplier = 0.55f,
            MuzzleLifetimeSeconds = 0.16f,
            ImpactLifetimeSeconds = 0.18f
        },
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_FireTower.asset",
            OutputName = "Fire",
            ProjectileSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Projectile_V1_03_black_fire.prefab",
            ImpactSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Impact_V1_03_black_fire.prefab",
            MuzzleSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_03_black_fire.prefab",
            RemovedImpactChildren = new[] { "SparksLong" },
            ImpactScaleMultiplier = 0.75f,
            ImpactLifetimeSeconds = 0.42f
        },
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_IceTower.asset",
            OutputName = "Ice",
            ProjectileSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Projectile_V1_26_blue_crystal.prefab",
            ImpactSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Impact_V1_26_blue_crystal.prefab",
            MuzzleSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_26_blue_crystal.prefab",
            ImpactScaleMultiplier = 0.65f,
            ImpactLifetimeSeconds = 0.3f
        },
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_StoneTower.asset",
            OutputName = "StoneMeteor",
            ProjectileSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Projectile_V1_18_nova_orange.prefab",
            ImpactSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Impact_V1_21_red_arrow.prefab",
            MuzzleSourcePath = "Assets/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_18_nova_orange.prefab",
            RemovedImpactChildren = new[] { "Lightning", "SparksUp" },
            RemovedMuzzleChildren = new[] { "Sparks" },
            ProjectileScaleMultiplier = 0.55f,
            ImpactScaleMultiplier = 0.95f,
            MuzzleScaleMultiplier = 0.65f,
            MuzzleLifetimeSeconds = 0.22f,
            ImpactLifetimeSeconds = 0.48f
        }
    };

    public static string BuildAssignAndValidate()
    {
        EnsureOutputFolder();
        var report = new StringBuilder();

        foreach (TowerEffectDefinition definition in DEFINITIONS)
        {
            string impactPath = $"{OUTPUT_FOLDER}/Impact_Tower_{definition.OutputName}.prefab";
            string muzzlePath = definition.RemovedMuzzleChildren.Length == 0
                ? definition.MuzzleSourcePath
                : $"{OUTPUT_FOLDER}/Muzzle_Tower_{definition.OutputName}.prefab";
            string projectilePath = $"{OUTPUT_FOLDER}/Projectile_Tower_{definition.OutputName}.prefab";

            CloneAndTrimEffect(
                definition.ImpactSourcePath,
                impactPath,
                $"Impact_Tower_{definition.OutputName}",
                definition.RemovedImpactChildren,
                definition.ImpactScaleMultiplier);

            if (definition.RemovedMuzzleChildren.Length != 0)
            {
                CloneAndTrimEffect(
                    definition.MuzzleSourcePath,
                    muzzlePath,
                    $"Muzzle_Tower_{definition.OutputName}",
                    definition.RemovedMuzzleChildren,
                    definition.MuzzleScaleMultiplier);
            }

            BuildProjectileVariant(definition, projectilePath, muzzlePath, impactPath);
            AssignTowerData(definition.TowerDataPath, projectilePath);
            report.AppendLine($"{definition.OutputName} -> {projectilePath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        ValidateSavedAssets();
        ValidateTimeTowerRemainsHitscan();

        report.Append("검증 완료: 타워 전용 투사체 7종, 축약 Impact 7종, Stone 전용 Muzzle, TimeTower 미변경");
        return report.ToString();
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/Projectile", "Tower");
        }
    }

    private static void CloneAndTrimEffect(
        string sourcePath,
        string destinationPath,
        string outputName,
        IReadOnlyCollection<string> removedChildNames,
        float scaleMultiplier)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(sourcePath);
        if (root == null)
        {
            throw new InvalidOperationException($"이펙트 원본을 열 수 없습니다: {sourcePath}");
        }

        try
        {
            root.name = outputName;
            RemoveNamedChildren(root, removedChildNames);
            root.transform.localScale *= scaleMultiplier;
            PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void BuildProjectileVariant(
        TowerEffectDefinition definition,
        string projectilePath,
        string muzzlePath,
        string impactPath)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(definition.ProjectileSourcePath);
        if (root == null)
        {
            throw new InvalidOperationException($"투사체 원본을 열 수 없습니다: {definition.ProjectileSourcePath}");
        }

        try
        {
            root.name = $"Projectile_Tower_{definition.OutputName}";
            root.transform.localScale *= definition.ProjectileScaleMultiplier;

            ProjectileVisual visual = root.GetComponent<ProjectileVisual>();
            if (visual == null)
            {
                throw new InvalidOperationException($"ProjectileVisual이 없습니다: {definition.ProjectileSourcePath}");
            }

            GameObject muzzlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(muzzlePath);
            GameObject impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(impactPath);
            var serializedVisual = new SerializedObject(visual);
            serializedVisual.FindProperty(MUZZLE_PROPERTY).objectReferenceValue = muzzlePrefab;
            serializedVisual.FindProperty(IMPACT_PROPERTY).objectReferenceValue = impactPrefab;
            serializedVisual.FindProperty(MUZZLE_LIFETIME_PROPERTY).floatValue = definition.MuzzleLifetimeSeconds;
            serializedVisual.FindProperty(IMPACT_LIFETIME_PROPERTY).floatValue = definition.ImpactLifetimeSeconds;
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, projectilePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveNamedChildren(GameObject root, IReadOnlyCollection<string> removedChildNames)
    {
        if (removedChildNames.Count == 0)
        {
            return;
        }

        HashSet<string> names = removedChildNames.ToHashSet();
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int index = transforms.Length - 1; index >= 0; index--)
        {
            Transform child = transforms[index];
            if (child != root.transform && names.Contains(child.name))
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void AssignTowerData(string towerDataPath, string projectilePath)
    {
        TowerData towerData = AssetDatabase.LoadAssetAtPath<TowerData>(towerDataPath);
        GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath);
        if (towerData == null || projectilePrefab == null)
        {
            throw new InvalidOperationException($"타워 배선 에셋을 찾을 수 없습니다: {towerDataPath}");
        }

        var serializedTowerData = new SerializedObject(towerData);
        serializedTowerData.FindProperty(PROJECTILE_PROPERTY).objectReferenceValue = projectilePrefab;
        serializedTowerData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(towerData);
    }

    private static void ValidateSavedAssets()
    {
        foreach (TowerEffectDefinition definition in DEFINITIONS)
        {
            string projectilePath = $"{OUTPUT_FOLDER}/Projectile_Tower_{definition.OutputName}.prefab";
            TowerData towerData = AssetDatabase.LoadAssetAtPath<TowerData>(definition.TowerDataPath);
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath);

            if (towerData == null || towerData.ProjectilePrefab != projectilePrefab || !towerData.HasProjectile)
            {
                throw new InvalidOperationException($"TowerData 참조 검증에 실패했습니다: {definition.TowerDataPath}");
            }

            ProjectileVisual visual = projectilePrefab.GetComponent<ProjectileVisual>();
            if (projectilePrefab.GetComponent<Projectile>() == null || visual == null)
            {
                throw new InvalidOperationException($"투사체 컴포넌트 검증에 실패했습니다: {projectilePath}");
            }

            var serializedVisual = new SerializedObject(visual);
            if (serializedVisual.FindProperty(MUZZLE_PROPERTY).objectReferenceValue == null ||
                serializedVisual.FindProperty(IMPACT_PROPERTY).objectReferenceValue == null)
            {
                throw new InvalidOperationException($"Muzzle/Impact 참조 검증에 실패했습니다: {projectilePath}");
            }
        }
    }

    private static void ValidateTimeTowerRemainsHitscan()
    {
        const string TIME_TOWER_PATH = "Assets/Data/TowerData/TD_TimeTower.asset";
        TowerData timeTower = AssetDatabase.LoadAssetAtPath<TowerData>(TIME_TOWER_PATH);
        if (timeTower == null || timeTower.ProjectilePrefab != null)
        {
            throw new InvalidOperationException("시간 타워의 비투사체 설정이 변경되었습니다.");
        }
    }
}
