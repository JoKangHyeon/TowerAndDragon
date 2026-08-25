using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 새끼용 5속성의 공격/버프 범위 프리팹을 원본을 보존한 채 만든다.
/// Coplay execute_script 전용 편집기 스크립트다.
/// </summary>
public static class BuildBabyDragonRangeVfx
{
    private const string SWORD_SOURCE_ROOT =
        "Assets/Imported/Hovl Studio_waypoints/Way points/Prefabs/";
    private const string LOOP_SOURCE_ROOT =
        "Assets/Imported/Hovl Studio_waypoints/Way points/Prefabs/Loop version/";
    private const string OUTPUT_ROOT =
        "Assets/Imported/Prefabs/BabyDragon/Range/";
    private const string DATA_ROOT = "Assets/Data/BabyDragon/";
    private const string BABY_DRAGON_PREFAB =
        "Assets/Prefabs/Building/BabyDragonTower.prefab";
    private const string REPORT_PATH =
        "output/vfx_check/baby_dragon_range_build.txt";

    private const string ATTACK_FIELD = "_attackRangeVfxPrefab";
    private const string BUFF_FIELD = "_buffRangeVfxPrefab";
    private const string MARKER_CHILD = "Marker";
    private const string RANGE_RING_PARTICLE_NAME = "Circle";
    private const string SCALE_OBJECT = "Scale";

    // 정확한 경계는 LineRenderer가 맡는다. 단일 파티클 링은 판정선의 약 95%에서
    // 끝나도록 두어 원본 1초 맥동이 경계 안쪽을 훑는 연출로 읽히게 한다.
    private const float NORMALIZED_XZ_SCALE = 0.95f;
    private const float ISOMETRIC_Y_SCALE = 0.475f;
    private const float SWORD_HIGHLIGHT_RATIO = 0.2f;
    private const float SWORD_REPEAT_SECONDS = 3f;
    private const string SWORD_SORTING_LAYER_NAME = "Overlay";
    private const int SORTING_LAYER_ID = 0;
    private const int SORTING_ORDER = 9;
    private const int SWORD_SORTING_ORDER = 11;

    private struct Entry
    {
        public DragonType Type;
        public string Key;
        public string SourceColor;
    }

    private static readonly Entry[] ENTRIES =
    {
        new Entry { Type = DragonType.Ice, Key = "Ice", SourceColor = "cyan" },
        new Entry { Type = DragonType.Fire, Key = "Fire", SourceColor = "red" },
        new Entry { Type = DragonType.Time, Key = "Time", SourceColor = "yellow" },
        new Entry { Type = DragonType.Stone, Key = "Stone", SourceColor = "violet" },
        new Entry { Type = DragonType.Life, Key = "Life", SourceColor = "green" },
    };

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");
        EnsureFolder("Assets/Imported/Prefabs", "BabyDragon");
        EnsureFolder("Assets/Imported/Prefabs/BabyDragon", "Range");

        var report = new StringBuilder();
        report.AppendLine("[BuildBabyDragonRangeVfx]");
        report.AppendLine(
            $"  Scale ({NORMALIZED_XZ_SCALE}, {ISOMETRIC_Y_SCALE}, {NORMALIZED_XZ_SCALE})");
        report.AppendLine($"  Sorting Default/{SORTING_ORDER}");
        report.AppendLine();

        foreach (Entry entry in ENTRIES)
        {
            Color tint = DragonAttributePalette.ColorOf(entry.Type);
            GameObject attack = BuildAttack(entry, tint, report);
            GameObject buff = BuildBuff(entry, tint, report);

            WireData(entry, attack, buff, report);
        }

        AddDisplayToBabyDragonPrefab(report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static GameObject BuildAttack(
        Entry entry, Color tint, StringBuilder report)
    {
        string sourcePath =
            LOOP_SOURCE_ROOT + "Marker circle simple " + entry.SourceColor + ".prefab";
        string swordSourcePath =
            SWORD_SOURCE_ROOT + "Marker circle swords " + entry.SourceColor + ".prefab";
        string outputPath = OUTPUT_ROOT + "FX_BD_AttackRange_" + entry.Key + ".prefab";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        GameObject swordSource =
            AssetDatabase.LoadAssetAtPath<GameObject>(swordSourcePath);

        if (source == null || swordSource == null)
        {
            report.AppendLine(
                $"[없음] ring={sourcePath}, sword={swordSourcePath}");
            return null;
        }

        GameObject root = Compose("FX_BD_AttackRange_" + entry.Key, source, out GameObject body);
        body.name = "StaticRangeRing";
        Transform sourceMarker = FindChild(swordSource.transform, MARKER_CHILD);

        if (sourceMarker == null)
        {
            report.AppendLine($"[실패] {swordSourcePath}: {MARKER_CHILD} 없음");
            Object.DestroyImmediate(root);
            return null;
        }

        GameObject markerObject = Object.Instantiate(
            sourceMarker.gameObject,
            root.transform);
        markerObject.name = MARKER_CHILD;
        Transform marker = markerObject.transform;
        marker.localPosition = sourceMarker.localPosition;
        marker.localRotation = sourceMarker.localRotation;
        marker.localScale = sourceMarker.localScale;

        ParticleSystem swordParticles = marker.GetComponent<ParticleSystem>();
        if (swordParticles != null)
        {
            ParticleSystem.MainModule main = swordParticles.main;
            main.duration = SWORD_REPEAT_SECONDS;

            ParticleSystem.EmissionModule emission = swordParticles.emission;
            emission.rateOverTime = 0f;
        }

        CorrectParticles(root, tint, marker);
        SynchronizeRangeRingPulse(root, swordParticles != null
            ? swordParticles.main.duration
            : SWORD_REPEAT_SECONDS);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
        Object.DestroyImmediate(root);

        report.AppendLine(
            $"  공격 {entry.Type,-5} ← loop simple + sword burst " +
            $"{entry.SourceColor,-6} / {outputPath}");
        return saved;
    }

    private static GameObject BuildBuff(
        Entry entry, Color tint, StringBuilder report)
    {
        string sourcePath =
            LOOP_SOURCE_ROOT + "Marker circle simple " + entry.SourceColor + ".prefab";
        string outputPath = OUTPUT_ROOT + "FX_BD_BuffRange_" + entry.Key + ".prefab";
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

        if (source == null)
        {
            report.AppendLine($"[없음] {sourcePath}");
            return null;
        }

        GameObject root = Compose("FX_BD_BuffRange_" + entry.Key, source, out _);
        CorrectParticles(root, tint, null);
        SynchronizeRangeRingPulse(root, SWORD_REPEAT_SECONDS);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
        Object.DestroyImmediate(root);

        report.AppendLine(
            $"  버프 {entry.Type,-5} ← simple {entry.SourceColor,-6} / {outputPath}");
        return saved;
    }

    private static GameObject Compose(
        string name, GameObject source, out GameObject body)
    {
        var root = new GameObject(name);
        var scale = new GameObject(SCALE_OBJECT);
        scale.transform.SetParent(root.transform, false);
        scale.transform.localScale = new Vector3(
            NORMALIZED_XZ_SCALE,
            ISOMETRIC_Y_SCALE,
            NORMALIZED_XZ_SCALE);

        body = Object.Instantiate(source, scale.transform);
        body.name = source.name;
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        return root;
    }

    private static void CorrectParticles(
        GameObject root,
        Color tint,
        Transform movingMarker)
    {
        foreach (ParticleSystem particles in
            root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startRotationX = 0f;

            bool isMarker =
                movingMarker != null && particles.transform == movingMarker;
            bool isRangeRing =
                !isMarker && particles.name == RANGE_RING_PARTICLE_NAME;

            // 범위 형태는 첫 프레임부터 최종 크기로 고정한다. 맥동은 원본
            // ColorOverLifetime만 남겨 밝기 변화로 표현한다.
            if (!isMarker)
            {
                ParticleSystem.SizeOverLifetimeModule size =
                    particles.sizeOverLifetime;
                size.enabled = false;

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = isRangeRing;
            }

            ApplyTint(
                particles,
                isMarker
                    ? Color.Lerp(tint, Color.white, SWORD_HIGHLIGHT_RATIO)
                    : tint);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();

            if (renderer != null)
            {
                renderer.enabled = isMarker || isRangeRing;
                renderer.sortingLayerID = isMarker
                    ? SortingLayer.NameToID(SWORD_SORTING_LAYER_NAME)
                    : SORTING_LAYER_ID;
                renderer.sortingOrder = isMarker
                    ? SWORD_SORTING_ORDER
                    : SORTING_ORDER;
            }
        }
    }

    private static void SynchronizeRangeRingPulse(
        GameObject root,
        float swordPeriodSeconds)
    {
        Transform circle = FindChild(root.transform, RANGE_RING_PARTICLE_NAME);
        ParticleSystem particles =
            circle != null ? circle.GetComponent<ParticleSystem>() : null;

        if (particles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        main.duration = swordPeriodSeconds;
        main.startLifetime = swordPeriodSeconds;
    }

    private static void ApplyTint(ParticleSystem particles, Color tint)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startColor = Recolor(main.startColor, tint);

        ParticleSystem.ColorOverLifetimeModule lifetime =
            particles.colorOverLifetime;
        if (lifetime.enabled)
        {
            lifetime.color = Recolor(lifetime.color, Color.white);
        }

        ParticleSystem.ColorBySpeedModule speed = particles.colorBySpeed;
        if (speed.enabled)
        {
            speed.color = Recolor(speed.color, Color.white);
        }
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
        GradientAlphaKey[] alphas = source.alphaKeys;

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i].color = Recolor(colors[i].color, tint);
        }

        var result = new Gradient { mode = source.mode };
        result.SetKeys(colors, alphas);
        return result;
    }

    private static Color Recolor(Color source, Color tint)
    {
        float brightness = Mathf.Max(source.r, source.g, source.b);
        return new Color(
            tint.r * brightness,
            tint.g * brightness,
            tint.b * brightness,
            source.a);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static void WireData(
        Entry entry,
        GameObject attack,
        GameObject buff,
        StringBuilder report)
    {
        string dataPath =
            DATA_ROOT + entry.Key + "/BD_" + entry.Key + ".asset";
        BabyDragonData data =
            AssetDatabase.LoadAssetAtPath<BabyDragonData>(dataPath);

        if (data == null)
        {
            report.AppendLine($"[없음] {dataPath}");
            return;
        }

        var serialized = new SerializedObject(data);
        serialized.FindProperty(ATTACK_FIELD).objectReferenceValue = attack;
        serialized.FindProperty(BUFF_FIELD).objectReferenceValue = buff;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
    }

    private static void AddDisplayToBabyDragonPrefab(StringBuilder report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BABY_DRAGON_PREFAB);

        if (root.GetComponent<BabyDragonRangeVfxDisplay>() == null)
        {
            root.AddComponent<BabyDragonRangeVfxDisplay>();
            report.AppendLine("  BabyDragonTower.prefab에 BabyDragonRangeVfxDisplay 추가");
        }

        PrefabUtility.SaveAsPrefabAsset(root, BABY_DRAGON_PREFAB);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
