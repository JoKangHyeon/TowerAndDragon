using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 원본 VFX는 건드리지 않고 새끼용 공격 전용 복합 프리팹을 생성·배선한다.
/// Coplay execute_script 전용 편집기 스크립트다.
/// </summary>
public static class BuildBabyDragonCombatVfx
{
    private const string OUTPUT_ROOT =
        "Assets/Imported/Prefabs/BabyDragonEffect/Combat/";
    private const string DATA_ROOT = "Assets/Data/BabyDragon/";
    private const string HEAL_EFFECT_ASSET =
        "Assets/Data/BabyDragon/Life/TA_BD_TowerHealEffect.asset";
    private const string REPORT_PATH =
        "output/vfx_check/baby_dragon_combat_build.txt";

    private const string PROJECTILE_FIELD = "_projectilePrefab";
    private const string TRAIL_FIELD = "_trailParticles";
    private const string MUZZLE_FIELD = "_muzzlePrefab";
    private const string MUZZLE_LIFETIME_FIELD = "_muzzleLifetimeSeconds";
    private const string IMPACT_FIELD = "_impactPrefab";
    private const string IMPACT_LIFETIME_FIELD = "_impactLifetimeSeconds";
    private const string IMPACT_ROTATION_FIELD =
        "_rotateImpactToTravelDirection";
    private const string IMPACT_PLACEMENT_FIELD = "_impactPlacement";
    private const string HEAL_VFX_FIELD = "_healVfxPrefab";
    private const string HEAL_VFX_LIFETIME_FIELD =
        "_healVfxLifetimeSeconds";

    private const string SORTING_LAYER = "Projectile";
    private const int MUZZLE_SORTING_ORDER = 19;
    private const int PROJECTILE_SORTING_ORDER = 20;
    private const int IMPACT_SORTING_ORDER = 21;

    private const string V1_ROOT =
        "Assets/Imported/Prefabs/Projectile/AAA_Vol1/";
    private const string V2_ROOT =
        "Assets/Imported/Prefabs/Projectile/AAA_Vol2/";
    private const string RETRO_ROOT =
        "Assets/Imported/Retro Arsenal/Prefabs/Combat/";
    private const string STATUS_ROOT =
        "Assets/Imported/Prefabs/Effects/Status/";

    private const string ICE_PROJECTILE_BASE =
        V1_ROOT + "Projectile_V1_06_blue_fire.prefab";
    private const string ICE_CORE =
        RETRO_ROOT + "Missiles/Frost/FrostMissile.prefab";
    private const string ICE_MUZZLE =
        RETRO_ROOT + "Muzzleflash/Frost/FrostMuzzle.prefab";
    private const string ICE_EXPLOSION =
        RETRO_ROOT + "Explosions/Frost/FrostExplosion.prefab";
    private const string ICE_SHARDS =
        V2_ROOT + "Impact/Impact_V2_05_ice.prefab";
    private const string ICE_CRACK = STATUS_ROOT + "FX_Impact_IceCrack.prefab";

    private const string FIRE_PROJECTILE_BASE =
        V1_ROOT + "Projectile_V1_16_fire.prefab";
    private const string FIRE_BREATH =
        RETRO_ROOT + "Flamethrower/DragonsBreath.prefab";
    private const string FIRE_MUZZLE =
        V1_ROOT + "Muzzle_V1_16_fire.prefab";
    private const string FIRE_EXPLOSION =
        V1_ROOT + "Impact_V1_16_fire.prefab";
    private const string FIRE_CRACK = STATUS_ROOT + "FX_Impact_FireCrack.prefab";

    private const string TIME_PROJECTILE_BASE =
        V2_ROOT + "Projectile_V2_25_yellow.prefab";
    private const string TIME_TRAIL_SOURCE =
        V2_ROOT + "Projectile_V2_09_trails.prefab";
    private const string TIME_MUZZLE =
        V2_ROOT + "Muzzle/Muzzle_V2_09_trails.prefab";
    private const string TIME_IMPACT =
        V2_ROOT + "Impact/Impact_V2_09_trails.prefab";

    private const string STONE_PROJECTILE_SHELL =
        V1_ROOT + "Projectile_V1_23_cube.prefab";
    private const string STONE_BODY =
        RETRO_ROOT + "Missiles/Earth/EarthMissile.prefab";
    private const string STONE_MUZZLE =
        RETRO_ROOT + "Muzzleflash/Earth/EarthMuzzle.prefab";
    private const string STONE_EXPLOSION =
        RETRO_ROOT + "Explosions/Earth/EarthExplosion.prefab";
    private const string STONE_CRACK =
        STATUS_ROOT + "FX_Impact_StoneCrack.prefab";

    private const string LIFE_PROJECTILE_BASE =
        V2_ROOT + "Projectile_V2_21_green.prefab";
    private const string LIFE_MUZZLE =
        V2_ROOT + "Muzzle/Muzzle_V2_21_green.prefab";
    private const string LIFE_ENERGY_IMPACT =
        V2_ROOT + "Impact/Impact_V2_21_green.prefab";
    private const string LIFE_NATURE_IMPACT =
        V2_ROOT + "Impact/Impact_V2_01_nature.prefab";
    private const string LIFE_HEAL_SOURCE =
        V2_ROOT + "Impact/Impact_V2_23_green.prefab";

    private const float DEFAULT_MUZZLE_LIFETIME = 0.45f;
    private const float ICE_IMPACT_LIFETIME = 1.1f;
    private const float FIRE_IMPACT_LIFETIME = 1f;
    private const float TIME_MUZZLE_LIFETIME = 0.25f;
    private const float TIME_IMPACT_LIFETIME = 0.45f;
    private const float STONE_IMPACT_LIFETIME = 1.25f;
    private const float LIFE_IMPACT_LIFETIME = 0.8f;
    private const float LIFE_HEAL_LIFETIME = 1.1f;

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");
        EnsureFolderRecursive(OUTPUT_ROOT.TrimEnd('/'));

        var report = new StringBuilder();
        report.AppendLine("[BuildBabyDragonCombatVfx]");
        report.AppendLine("  출력: " + OUTPUT_ROOT);
        report.AppendLine("  원본 프리팹은 수정하지 않음");
        report.AppendLine();

        BuildIce(report);
        BuildFire(report);
        BuildTime(report);
        BuildStone(report);
        BuildLife(report);
        BuildLifeHealWave(report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static void BuildIce(StringBuilder report)
    {
        Color tint = DragonAttributePalette.ColorOf(DragonType.Ice);
        GameObject muzzle = BuildSingleEffect(
            ICE_MUZZLE, "Muzzle_BD_Ice", tint, 0.8f, 1.05f,
            1.1f, MUZZLE_SORTING_ORDER, true, report);

        GameObject impactRoot = new GameObject("Impact_BD_Ice");
        AttachNamedLayer(impactRoot, ICE_EXPLOSION, "Snowflakes");
        AttachNamedLayer(impactRoot, ICE_SHARDS, "Snowflake");
        AttachFullLayer(impactRoot, ICE_CRACK, "GroundCrack", 0.85f);
        TuneParticles(impactRoot, tint, 1.05f, 1.15f, 1.15f,
            IMPACT_SORTING_ORDER, false);
        GameObject impact = Save(impactRoot, "Impact_BD_Ice", report);

        GameObject projectile = CreateProjectileShell(
            ICE_PROJECTILE_BASE, "Projectile_BD_Ice");
        GameObject core = AttachFullLayer(
            projectile, ICE_CORE, "FrostCore", 0.58f);
        RemoveNamedDescendant(core, "SmokeTrail");
        TuneParticles(projectile, tint, 1.08f, 1.15f, 1.05f,
            PROJECTILE_SORTING_ORDER, true);
        ConfigureProjectileVisual(
            projectile, muzzle, impact, DEFAULT_MUZZLE_LIFETIME,
            ICE_IMPACT_LIFETIME, ProjectileImpactPlacement.Ground, false);
        WireAndSave(DragonType.Ice, "Ice", projectile, report);
    }

    private static void BuildFire(StringBuilder report)
    {
        Color tint = DragonAttributePalette.ColorOf(DragonType.Fire);
        GameObject muzzle = BuildSingleEffect(
            FIRE_MUZZLE, "Muzzle_BD_Fire", tint, 1.05f, 1.1f,
            1.15f, MUZZLE_SORTING_ORDER, true, report);

        GameObject impactRoot = new GameObject("Impact_BD_Fire");
        AttachFullLayer(impactRoot, FIRE_EXPLOSION, "FlameBloom", 0.75f);
        AttachFullLayer(impactRoot, FIRE_CRACK, "GroundCrack", 0.9f);
        TuneParticles(impactRoot, tint, 1.05f, 1.2f, 1.15f,
            IMPACT_SORTING_ORDER, false);
        GameObject impact = Save(impactRoot, "Impact_BD_Fire", report);

        GameObject projectile = CreateProjectileShell(
            FIRE_PROJECTILE_BASE, "Projectile_BD_Fire");
        GameObject breath = AttachFullLayer(
            projectile, FIRE_BREATH, "DragonBreathTrail", 0.32f);
        RemoveNamedDescendant(breath, "FireExplosion");
        TuneParticles(projectile, tint, 1.08f, 1.2f, 1.05f,
            PROJECTILE_SORTING_ORDER, true);
        ConfigureProjectileVisual(
            projectile, muzzle, impact, DEFAULT_MUZZLE_LIFETIME,
            FIRE_IMPACT_LIFETIME, ProjectileImpactPlacement.Ground, false);
        WireAndSave(DragonType.Fire, "Fire", projectile, report);
    }

    private static void BuildTime(StringBuilder report)
    {
        Color tint = DragonAttributePalette.ColorOf(DragonType.Time);
        GameObject muzzle = BuildSingleEffect(
            TIME_MUZZLE, "Muzzle_BD_Time", tint, 0.75f, 0.95f,
            1f, MUZZLE_SORTING_ORDER, true, report);
        GameObject impact = BuildSingleEffect(
            TIME_IMPACT, "Impact_BD_Time", tint, 0.55f, 0.9f,
            0.9f, IMPACT_SORTING_ORDER, false, report);

        GameObject projectile = CreateProjectileShell(
            TIME_PROJECTILE_BASE, "Projectile_BD_Time");
        AddTimeAfterimages(projectile);
        AttachNamedLayer(projectile, TIME_TRAIL_SOURCE, "Sparks");
        projectile.transform.localScale *= 1.05f;
        TuneParticles(projectile, tint, 1.08f, 1.15f, 1f,
            PROJECTILE_SORTING_ORDER, true);
        ConfigureProjectileVisual(
            projectile, muzzle, impact, TIME_MUZZLE_LIFETIME,
            TIME_IMPACT_LIFETIME, ProjectileImpactPlacement.Body, true);
        WireAndSave(DragonType.Time, "Time", projectile, report);
    }

    private static void BuildStone(StringBuilder report)
    {
        Color tint = DragonAttributePalette.ColorOf(DragonType.Stone);
        GameObject muzzle = BuildSingleEffect(
            STONE_MUZZLE, "Muzzle_BD_Stone", tint, 0.9f, 1.15f,
            1.2f, MUZZLE_SORTING_ORDER, true, report);

        GameObject impactRoot = new GameObject("Impact_BD_Stone");
        AttachFullLayer(impactRoot, STONE_EXPLOSION, "RockDebris", 0.95f);
        AttachFullLayer(impactRoot, STONE_CRACK, "GroundCrack", 1f);
        TuneParticles(impactRoot, tint, 1.15f, 1.15f, 1.25f,
            IMPACT_SORTING_ORDER, false);
        GameObject impact = Save(impactRoot, "Impact_BD_Stone", report);

        GameObject projectile = CreateProjectileShell(
            STONE_PROJECTILE_SHELL, "Projectile_BD_Stone");
        ClearProjectileVisuals(projectile);
        AttachFullLayer(projectile, STONE_BODY, "RockBody", 0.8f);
        projectile.transform.localScale *= 1.08f;
        TuneParticles(projectile, tint, 1.12f, 1.15f, 1.05f,
            PROJECTILE_SORTING_ORDER, true);
        ConfigureProjectileVisual(
            projectile, muzzle, impact, DEFAULT_MUZZLE_LIFETIME,
            STONE_IMPACT_LIFETIME, ProjectileImpactPlacement.Ground, false);
        WireAndSave(DragonType.Stone, "Stone", projectile, report);
    }

    private static void BuildLife(StringBuilder report)
    {
        Color tint = DragonAttributePalette.ColorOf(DragonType.Life);
        GameObject muzzle = BuildSingleEffect(
            LIFE_MUZZLE, "Muzzle_BD_Life", tint, 0.8f, 1f,
            1.05f, MUZZLE_SORTING_ORDER, true, report);

        GameObject impactRoot = new GameObject("Impact_BD_Life");
        AttachNamedLayer(impactRoot, LIFE_ENERGY_IMPACT, "Lightning");
        AttachNamedLayer(impactRoot, LIFE_ENERGY_IMPACT, "Fire");
        AttachNamedLayer(impactRoot, LIFE_ENERGY_IMPACT, "Sparks");
        AttachNamedLayer(impactRoot, LIFE_NATURE_IMPACT, "Flash");
        AttachNamedLayer(impactRoot, LIFE_NATURE_IMPACT, "Leaves");
        AttachNamedLayer(impactRoot, LIFE_NATURE_IMPACT, "Leaves2");
        AttachNamedLayer(impactRoot, LIFE_NATURE_IMPACT, "Glow");
        impactRoot.transform.localScale *= 0.72f;
        TuneParticles(impactRoot, tint, 1.05f, 1.1f, 1.1f,
            IMPACT_SORTING_ORDER, false);
        GameObject impact = Save(impactRoot, "Impact_BD_Life", report);

        GameObject projectile = CreateProjectileShell(
            LIFE_PROJECTILE_BASE, "Projectile_BD_Life");
        RemoveNamedDescendant(projectile, "Darkness");
        projectile.transform.localScale *= 0.95f;
        TuneParticles(projectile, tint, 1.1f, 1.15f, 1.05f,
            PROJECTILE_SORTING_ORDER, true);
        ConfigureProjectileVisual(
            projectile, muzzle, impact, DEFAULT_MUZZLE_LIFETIME,
            LIFE_IMPACT_LIFETIME, ProjectileImpactPlacement.Body, false);
        WireAndSave(DragonType.Life, "Life", projectile, report);
    }

    private static void BuildLifeHealWave(StringBuilder report)
    {
        Color tint = DragonAttributePalette.ColorOf(DragonType.Life);
        GameObject root = new GameObject("FX_BD_LifeHealWave");
        AttachNamedLayer(root, LIFE_HEAL_SOURCE, "Flash");
        AttachNamedLayer(root, LIFE_HEAL_SOURCE, "Sparks");
        AttachNamedLayer(root, LIFE_HEAL_SOURCE, "ShockWave");
        root.transform.localScale *= 1.35f;
        TuneParticles(root, tint, 1.05f, 0.9f, 1f,
            IMPACT_SORTING_ORDER, false);
        GameObject saved = Save(root, "FX_BD_LifeHealWave", report);

        TowerHealAuraEffectSO heal =
            AssetDatabase.LoadAssetAtPath<TowerHealAuraEffectSO>(
                HEAL_EFFECT_ASSET);
        if (heal == null)
        {
            report.AppendLine("[FAIL] 회복 효과 데이터 없음: " + HEAL_EFFECT_ASSET);
            return;
        }

        var serialized = new SerializedObject(heal);
        serialized.FindProperty(HEAL_VFX_FIELD).objectReferenceValue = saved;
        serialized.FindProperty(HEAL_VFX_LIFETIME_FIELD).floatValue =
            LIFE_HEAL_LIFETIME;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(heal);
        report.AppendLine("  생명 회복 파동 배선 완료");
    }

    private static GameObject BuildSingleEffect(
        string sourcePath,
        string outputName,
        Color tint,
        float rootScale,
        float sizeScale,
        float emissionScale,
        int sortingOrder,
        bool worldSimulation,
        StringBuilder report)
    {
        GameObject instance = InstantiateUnpacked(Load(sourcePath));
        instance.name = outputName;
        instance.transform.localScale *= rootScale;
        StripVisualOnly(instance);
        TuneParticles(instance, tint, sizeScale, emissionScale, emissionScale,
            sortingOrder, worldSimulation);
        return Save(instance, outputName, report);
    }

    private static GameObject CreateProjectileShell(
        string sourcePath, string outputName)
    {
        GameObject instance = InstantiateUnpacked(Load(sourcePath));
        instance.name = outputName;
        StripForeignBehaviours(instance);

        if (instance.GetComponent<Projectile>() == null ||
            instance.GetComponent<ProjectileVisual>() == null)
        {
            Object.DestroyImmediate(instance);
            throw new InvalidDataException(
                sourcePath + "에 Projectile/ProjectileVisual이 없습니다.");
        }

        return instance;
    }

    private static GameObject AttachFullLayer(
        GameObject parent, string sourcePath, string layerName, float scale)
    {
        GameObject layer = InstantiateUnpacked(Load(sourcePath));
        layer.name = layerName;
        layer.transform.SetParent(parent.transform, false);
        layer.transform.localPosition = Vector3.zero;
        layer.transform.localRotation = Quaternion.identity;
        layer.transform.localScale *= scale;
        StripVisualOnly(layer);
        return layer;
    }

    private static void AttachNamedLayer(
        GameObject parent, string sourcePath, string childName)
    {
        GameObject source = InstantiateUnpacked(Load(sourcePath));
        Transform child = FindDescendant(source.transform, childName);
        if (child == null)
        {
            Object.DestroyImmediate(source);
            throw new InvalidDataException(
                sourcePath + "에서 자식을 찾지 못했습니다: " + childName);
        }

        GameObject layer = Object.Instantiate(
            child.gameObject, parent.transform, false);
        layer.name = childName;
        StripVisualOnly(layer);
        Object.DestroyImmediate(source);
    }

    private static void ConfigureProjectileVisual(
        GameObject projectile,
        GameObject muzzle,
        GameObject impact,
        float muzzleLifetime,
        float impactLifetime,
        ProjectileImpactPlacement placement,
        bool rotateImpact)
    {
        ProjectileVisual visual = projectile.GetComponent<ProjectileVisual>();
        var serialized = new SerializedObject(visual);
        serialized.FindProperty(TRAIL_FIELD).arraySize = 0;
        serialized.FindProperty(MUZZLE_FIELD).objectReferenceValue = muzzle;
        serialized.FindProperty(MUZZLE_LIFETIME_FIELD).floatValue = muzzleLifetime;
        serialized.FindProperty(IMPACT_FIELD).objectReferenceValue = impact;
        serialized.FindProperty(IMPACT_LIFETIME_FIELD).floatValue = impactLifetime;
        serialized.FindProperty(IMPACT_ROTATION_FIELD).boolValue = rotateImpact;
        serialized.FindProperty(IMPACT_PLACEMENT_FIELD).enumValueIndex =
            (int)placement;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddTimeAfterimages(GameObject projectile)
    {
        Transform primary = FindDescendant(projectile.transform, "Projectile");
        if (primary == null)
        {
            throw new InvalidDataException("시간 투사체 코어를 찾지 못했습니다.");
        }

        GameObject near = Object.Instantiate(
            primary.gameObject, primary.parent, false);
        near.name = "AfterimageNear";
        near.transform.localPosition += new Vector3(-0.35f, 0f, 0f);
        near.transform.localScale *= 0.68f;

        GameObject far = Object.Instantiate(
            primary.gameObject, primary.parent, false);
        far.name = "AfterimageFar";
        far.transform.localPosition += new Vector3(-0.65f, 0f, 0f);
        far.transform.localScale *= 0.4f;
    }

    private static void WireAndSave(
        DragonType type,
        string key,
        GameObject projectileInstance,
        StringBuilder report)
    {
        GameObject projectile = Save(
            projectileInstance, "Projectile_BD_" + key, report);
        string dataPath = DATA_ROOT + key + "/BD_" + key + ".asset";
        BabyDragonData data =
            AssetDatabase.LoadAssetAtPath<BabyDragonData>(dataPath);
        if (data == null)
        {
            report.AppendLine("[FAIL] 데이터 없음: " + dataPath);
            return;
        }

        var serialized = new SerializedObject(data);
        serialized.FindProperty(PROJECTILE_FIELD).objectReferenceValue =
            projectile;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        report.AppendLine("  " + type + " 데이터 배선 완료");
        report.AppendLine();
    }

    private static GameObject Save(
        GameObject instance, string outputName, StringBuilder report)
    {
        string path = OUTPUT_ROOT + outputName + ".prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        if (saved == null)
        {
            throw new InvalidDataException("프리팹 저장 실패: " + path);
        }

        report.AppendLine("  저장 " + path);
        return saved;
    }

    private static GameObject Load(string path)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null)
        {
            throw new FileNotFoundException("VFX 원본을 찾지 못했습니다.", path);
        }
        return asset;
    }

    private static GameObject InstantiateUnpacked(GameObject source)
    {
        GameObject instance = Object.Instantiate(source);
        instance.name = source.name;
        if (PrefabUtility.IsPartOfPrefabInstance(instance))
        {
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
        }
        return instance;
    }

    private static void ClearProjectileVisuals(GameObject root)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        }

        ParticleSystemRenderer renderer =
            root.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Object.DestroyImmediate(renderer);
        }

        ParticleSystem particles = root.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            Object.DestroyImmediate(particles);
        }
    }

    private static void StripForeignBehaviours(GameObject root)
    {
        MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is Projectile || behaviour is ProjectileVisual)
            {
                continue;
            }
            Object.DestroyImmediate(behaviour);
        }

        StripAudioAndPhysics(root);
    }

    private static void StripVisualOnly(GameObject root)
    {
        MonoBehaviour[] behaviours =
            root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            Object.DestroyImmediate(behaviour);
        }
        StripAudioAndPhysics(root);
    }

    private static void StripAudioAndPhysics(GameObject root)
    {
        foreach (AudioSource source in
            root.GetComponentsInChildren<AudioSource>(true))
        {
            Object.DestroyImmediate(source);
        }
        foreach (Collider collider in
            root.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }
        foreach (Collider2D collider in
            root.GetComponentsInChildren<Collider2D>(true))
        {
            Object.DestroyImmediate(collider);
        }
        foreach (Rigidbody body in
            root.GetComponentsInChildren<Rigidbody>(true))
        {
            Object.DestroyImmediate(body);
        }
        foreach (Rigidbody2D body in
            root.GetComponentsInChildren<Rigidbody2D>(true))
        {
            Object.DestroyImmediate(body);
        }
    }

    private static void RemoveNamedDescendant(GameObject root, string name)
    {
        Transform target = FindDescendant(root.transform, name);
        if (target != null && target != root.transform)
        {
            Object.DestroyImmediate(target.gameObject);
        }
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant != root && descendant.name == name)
            {
                return descendant;
            }
        }
        return null;
    }

    private static void TuneParticles(
        GameObject root,
        Color tint,
        float sizeScale,
        float emissionScale,
        float burstScale,
        int sortingOrder,
        bool worldSimulation)
    {
        ParticleSystem[] systems =
            root.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particles in systems)
        {
            ParticleSystem.MainModule main = particles.main;
            main.startSize = Multiply(main.startSize, sizeScale);
            main.startColor = Recolor(main.startColor, tint);
            main.stopAction = ParticleSystemStopAction.None;
            // 코어는 Local, 잔상은 World를 쓰는 원본이 섞여 있으므로
            // 일괄 변경하지 않고 각 파티클의 검증된 Simulation Space를 보존한다.

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTimeMultiplier *= emissionScale;
            emission.rateOverDistanceMultiplier *= emissionScale;
            ScaleBursts(emission, burstScale);

            ParticleSystem.ColorOverLifetimeModule lifetime =
                particles.colorOverLifetime;
            if (lifetime.enabled)
            {
                lifetime.color = Recolor(lifetime.color, tint);
            }

            ParticleSystem.ColorBySpeedModule speed = particles.colorBySpeed;
            if (speed.enabled)
            {
                speed.color = Recolor(speed.color, tint);
            }

            ParticleSystemRenderer renderer =
                particles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sortingLayerName = SORTING_LAYER;
                renderer.sortingOrder = sortingOrder;
            }
        }
    }

    private static void ScaleBursts(
        ParticleSystem.EmissionModule emission, float scale)
    {
        int count = emission.burstCount;
        if (count == 0 || Mathf.Approximately(scale, 1f))
        {
            return;
        }

        var bursts = new ParticleSystem.Burst[count];
        emission.GetBursts(bursts);
        for (int i = 0; i < bursts.Length; i++)
        {
            ParticleSystem.Burst burst = bursts[i];
            burst.count = Multiply(burst.count, scale);
            bursts[i] = burst;
        }
        emission.SetBursts(bursts);
    }

    private static ParticleSystem.MinMaxCurve Multiply(
        ParticleSystem.MinMaxCurve curve, float factor)
    {
        if (curve.mode == ParticleSystemCurveMode.TwoConstants)
        {
            curve.constantMin *= factor;
            curve.constantMax *= factor;
        }
        else
        {
            curve.curveMultiplier *= factor;
        }
        return curve;
    }

    private static ParticleSystem.MinMaxGradient Recolor(
        ParticleSystem.MinMaxGradient source, Color tint)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    Recolor(source.colorMin, tint),
                    Recolor(source.colorMax, tint));
            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(
                    Recolor(source.gradient, tint));
            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    Recolor(source.gradientMin, tint),
                    Recolor(source.gradientMax, tint));
            default:
                return new ParticleSystem.MinMaxGradient(
                    Recolor(source.color, tint));
        }
    }

    private static Gradient Recolor(Gradient source, Color tint)
    {
        if (source == null)
        {
            return null;
        }

        GradientColorKey[] colors = source.colorKeys;
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i].color = Recolor(colors[i].color, tint);
        }
        var result = new Gradient { mode = source.mode };
        result.SetKeys(colors, source.alphaKeys);
        return result;
    }

    private static Color Recolor(Color source, Color tint)
    {
        float brightness = Mathf.Max(source.r, source.g, source.b);
        float intensity = Mathf.Lerp(0.65f, 1.65f, brightness);
        float highlight = Mathf.InverseLerp(0.72f, 1f, brightness) * 0.2f;
        Color colored = tint * intensity;
        Color result = Color.Lerp(colored, Color.white * intensity, highlight);
        result.a = source.a;
        return result;
    }

    private static void EnsureFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
