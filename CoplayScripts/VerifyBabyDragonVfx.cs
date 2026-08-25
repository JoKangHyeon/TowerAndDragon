using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>새끼용 범위·전투 VFX의 구조와 렌더 결과를 검증한다.</summary>
public static class VerifyBabyDragonVfx
{
    private const string RANGE_ROOT =
        "Assets/Imported/Prefabs/BabyDragon/Range/";
    private const string COMBAT_ROOT =
        "Assets/Imported/Prefabs/BabyDragonEffect/Combat/";
    private const string DATA_ROOT = "Assets/Data/BabyDragon/";
    private const string BABY_DRAGON_PREFAB =
        "Assets/Prefabs/Building/BabyDragonTower.prefab";
    private const string REPORT_PATH =
        "output/vfx_check/baby_dragon_vfx_verify.txt";
    private const string SHEET_PATH =
        "output/vfx_check/baby_dragon_range_sheet.png";

    private const string SCALE_OBJECT = "Scale";
    private const string STATIC_RING_OBJECT = "StaticRangeRing";
    private const string MARKER_CHILD = "Marker";
    private const string RANGE_RING_PARTICLE_NAME = "Circle";
    private const string SWORD_SORTING_LAYER_NAME = "Overlay";
    private const int SORTING_LAYER_ID = 0;
    private const int SORTING_ORDER = 9;
    private const int SWORD_SORTING_ORDER = 11;
    private const float EXPECTED_NORMALIZED_XZ_SCALE = 0.95f;
    private const float EXPECTED_ISOMETRIC_Y_SCALE = 0.475f;
    private const int PREVIEW_LAYER = 31;
    private const int FRAME_SIZE = 384;
    private const float ORTHO_SIZE = 7f;
    private const float CAMERA_DISTANCE = 10f;
    private const float SAMPLE_TIME = 30.5f;
    private const uint FIXED_SEED = 24680;

    private struct Entry
    {
        public DragonType Type;
        public string Key;
        public float AttackRadius;
        public float BuffRadius;
    }

    private static readonly Entry[] ENTRIES =
    {
        new Entry { Type = DragonType.Ice, Key = "Ice", AttackRadius = 4f, BuffRadius = 3f },
        new Entry { Type = DragonType.Fire, Key = "Fire", AttackRadius = 4f, BuffRadius = 5f },
        new Entry { Type = DragonType.Time, Key = "Time", AttackRadius = 5f, BuffRadius = 5f },
        new Entry { Type = DragonType.Stone, Key = "Stone", AttackRadius = 6f, BuffRadius = 3f },
        new Entry { Type = DragonType.Life, Key = "Life", AttackRadius = 4f, BuffRadius = 3f },
    };

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");

        var report = new StringBuilder();
        int failures = 0;

        report.AppendLine("[VerifyBabyDragonVfx]");
        report.AppendLine();

        GameObject cameraHost = CreateCamera(out Camera camera);
        var target = new RenderTexture(
            FRAME_SIZE,
            FRAME_SIZE,
            24,
            RenderTextureFormat.ARGB32);
        var readback = new Texture2D(
            FRAME_SIZE,
            FRAME_SIZE,
            TextureFormat.RGBA32,
            false);
        var sheet = new Texture2D(
            FRAME_SIZE * 2,
            FRAME_SIZE * ENTRIES.Length,
            TextureFormat.RGBA32,
            false);
        camera.targetTexture = target;

        for (int i = 0; i < ENTRIES.Length; i++)
        {
            Entry entry = ENTRIES[i];
            string attackPath =
                RANGE_ROOT + "FX_BD_AttackRange_" + entry.Key + ".prefab";
            string buffPath =
                RANGE_ROOT + "FX_BD_BuffRange_" + entry.Key + ".prefab";
            GameObject attack =
                AssetDatabase.LoadAssetAtPath<GameObject>(attackPath);
            GameObject buff =
                AssetDatabase.LoadAssetAtPath<GameObject>(buffPath);

            report.AppendLine($"=== {entry.Type}");
            failures += VerifyRangePrefab(attack, true, report);
            failures += VerifyRangePrefab(buff, false, report);
            failures += VerifyPulseSync(attack, buff, report);
            failures += VerifyData(entry, attack, buff, report);
            failures += VerifyCombat(entry, report);

            Color[] attackPixels = Bake(
                attack,
                entry.AttackRadius,
                camera,
                target,
                readback);
            Color[] buffPixels = Bake(
                buff,
                entry.BuffRadius,
                camera,
                target,
                readback);

            AppendAlignmentMetrics(
                report,
                "공격",
                attackPixels,
                entry.AttackRadius);
            AppendAlignmentMetrics(
                report,
                "버프",
                buffPixels,
                entry.BuffRadius);

            int row = ENTRIES.Length - 1 - i;
            sheet.SetPixels(
                0,
                row * FRAME_SIZE,
                FRAME_SIZE,
                FRAME_SIZE,
                attackPixels);
            sheet.SetPixels(
                FRAME_SIZE,
                row * FRAME_SIZE,
                FRAME_SIZE,
                FRAME_SIZE,
                buffPixels);
            report.AppendLine();
        }

        GameObject babyPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(BABY_DRAGON_PREFAB);
        bool hasDisplay =
            babyPrefab != null &&
            babyPrefab.GetComponent<BabyDragonRangeVfxDisplay>() != null;
        report.AppendLine(
            hasDisplay
                ? "[PASS] BabyDragonTower 표시 컴포넌트"
                : "[FAIL] BabyDragonTower 표시 컴포넌트 없음");
        if (!hasDisplay)
        {
            failures++;
        }

        sheet.Apply();
        File.WriteAllBytes(SHEET_PATH, sheet.EncodeToPNG());

        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(cameraHost);
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(readback);
        Object.DestroyImmediate(sheet);

        report.AppendLine();
        report.AppendLine($"결과: 실패 {failures}");
        report.AppendLine("시트: 왼쪽 공격 / 오른쪽 버프, 위에서 얼음·불·시간·암석·생명");

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static int VerifyRangePrefab(
        GameObject prefab,
        bool isAttack,
        StringBuilder report)
    {
        if (prefab == null)
        {
            report.AppendLine("[FAIL] 범위 프리팹 없음");
            return 1;
        }

        int failures = 0;
        Transform scale = prefab.transform.Find(SCALE_OBJECT);
        bool scaleMatches = scale != null &&
            Mathf.Approximately(
                scale.localScale.x,
                EXPECTED_NORMALIZED_XZ_SCALE) &&
            Mathf.Approximately(
                scale.localScale.y,
                EXPECTED_ISOMETRIC_Y_SCALE) &&
            Mathf.Approximately(
                scale.localScale.z,
                EXPECTED_NORMALIZED_XZ_SCALE);

        if (!scaleMatches)
        {
            report.AppendLine($"[FAIL] {prefab.name}: 아이소 Scale");
            failures++;
        }

        foreach (ParticleSystem particles in
            prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            ParticleSystemRenderer renderer =
                particles.GetComponent<ParticleSystemRenderer>();
            bool isSword =
                particles.name == MARKER_CHILD &&
                particles.transform.parent == prefab.transform;
            bool isRangeRing =
                !isSword && particles.name == RANGE_RING_PARTICLE_NAME;
            ParticleSystem.SizeOverLifetimeModule size =
                particles.sizeOverLifetime;
            ParticleSystem.ColorOverLifetimeModule color =
                particles.colorOverLifetime;
            ParticleSystem.EmissionModule emission = particles.emission;
            int expectedSortingOrder =
                isSword ? SWORD_SORTING_ORDER : SORTING_ORDER;
            int expectedSortingLayer = isSword
                ? SortingLayer.NameToID(SWORD_SORTING_LAYER_NAME)
                : SORTING_LAYER_ID;

            if (!main.loop ||
                main.simulationSpace != ParticleSystemSimulationSpace.Local ||
                !Mathf.Approximately(main.startRotationX.constantMax, 0f) ||
                renderer == null ||
                renderer.sortingLayerID != expectedSortingLayer ||
                (!isSword && renderer.sortingOrder != expectedSortingOrder) ||
                renderer.enabled != (isSword || isRangeRing) ||
                emission.enabled != (isSword || isRangeRing) ||
                (!isSword && size.enabled) ||
                (!isSword && !color.enabled))
            {
                report.AppendLine(
                    $"[FAIL] {prefab.name}/{particles.name}: 파티클 공통 설정 " +
                    $"loop={main.loop}, space={main.simulationSpace}, " +
                    $"rotX={main.startRotationX.constantMax}, renderer={renderer != null}, " +
                    $"layer={renderer?.sortingLayerID}/{expectedSortingLayer}, " +
                    $"order={renderer?.sortingOrder}, " +
                    $"expected={expectedSortingOrder}, sword={isSword}");
                failures++;
            }
        }

        if (isAttack)
        {
            Transform ring = scale?.Find(STATIC_RING_OBJECT);
            Transform marker = prefab.transform.Find(MARKER_CHILD);
            ParticleSystem swordParticles =
                marker != null ? marker.GetComponent<ParticleSystem>() : null;
            ParticleSystem.EmissionModule swordEmission =
                swordParticles != null ? swordParticles.emission : default;
            ParticleSystem.VelocityOverLifetimeModule swordVelocity =
                swordParticles != null
                    ? swordParticles.velocityOverLifetime
                    : default;
            bool attackStructure =
                ring != null &&
                swordParticles != null &&
                marker.parent == prefab.transform &&
                swordEmission.burstCount > 0 &&
                Mathf.Approximately(
                    swordEmission.rateOverTime.constantMax,
                    0f) &&
                swordVelocity.enabled &&
                swordVelocity.y.constantMax > 0f;

            if (!attackStructure)
            {
                report.AppendLine($"[FAIL] {prefab.name}: Sword 독립 루프 구조");
                failures++;
            }
        }

        report.AppendLine(
            failures == 0
                ? $"[PASS] {prefab.name}"
                : $"[FAIL] {prefab.name}: {failures}건");
        return failures;
    }

    private static int VerifyPulseSync(
        GameObject attack,
        GameObject buff,
        StringBuilder report)
    {
        ParticleSystem sword = FindParticle(attack, MARKER_CHILD);
        ParticleSystem attackCircle =
            FindParticle(attack, RANGE_RING_PARTICLE_NAME);
        ParticleSystem buffCircle =
            FindParticle(buff, RANGE_RING_PARTICLE_NAME);

        if (sword == null || attackCircle == null || buffCircle == null)
        {
            report.AppendLine("[FAIL] 검/링 맥동 파티클 없음");
            return 1;
        }

        float period = sword.main.duration;
        bool matches =
            Mathf.Approximately(attackCircle.main.duration, period) &&
            Mathf.Approximately(
                attackCircle.main.startLifetime.constantMax,
                period) &&
            Mathf.Approximately(buffCircle.main.duration, period) &&
            Mathf.Approximately(
                buffCircle.main.startLifetime.constantMax,
                period);

        report.AppendLine(
            matches
                ? $"[PASS] 검 기준 맥동 동기화 {period:F3}s"
                : $"[FAIL] 검 기준 맥동 동기화 {period:F3}s");
        return matches ? 0 : 1;
    }

    private static ParticleSystem FindParticle(
        GameObject root,
        string particleName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (ParticleSystem particles in
            root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particles.name == particleName)
            {
                return particles;
            }
        }

        return null;
    }

    private static int VerifyData(
        Entry entry,
        GameObject attack,
        GameObject buff,
        StringBuilder report)
    {
        string path =
            DATA_ROOT + entry.Key + "/BD_" + entry.Key + ".asset";
        BabyDragonData data =
            AssetDatabase.LoadAssetAtPath<BabyDragonData>(path);
        bool valid =
            data != null &&
            data.AttackRangeVfxPrefab == attack &&
            data.BuffRangeVfxPrefab == buff &&
            data.ProjectilePrefab != null &&
            data.ProjectilePrefab.name == "Projectile_BD_" + entry.Key &&
            AssetDatabase.GetAssetPath(data.ProjectilePrefab) ==
                COMBAT_ROOT + "Projectile_BD_" + entry.Key + ".prefab";

        report.AppendLine(
            valid
                ? "[PASS] 데이터 배선"
                : "[FAIL] 데이터 배선");
        return valid ? 0 : 1;
    }

    private static int VerifyCombat(Entry entry, StringBuilder report)
    {
        string path = COMBAT_ROOT + "Projectile_BD_" + entry.Key + ".prefab";
        GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        ProjectileVisual visual =
            projectile != null ? projectile.GetComponent<ProjectileVisual>() : null;
        GameObject muzzle = null;
        GameObject impact = null;
        if (visual != null)
        {
            var serialized = new SerializedObject(visual);
            muzzle = serialized.FindProperty("_muzzlePrefab").objectReferenceValue
                as GameObject;
            impact = serialized.FindProperty("_impactPrefab").objectReferenceValue
                as GameObject;
        }

        bool valid =
            projectile != null &&
            projectile.GetComponent<Projectile>() != null &&
            visual != null &&
            projectile.GetComponentsInChildren<ParticleSystem>(true).Length > 0 &&
            AssetDatabase.GetAssetPath(muzzle) ==
                COMBAT_ROOT + "Muzzle_BD_" + entry.Key + ".prefab" &&
            AssetDatabase.GetAssetPath(impact) ==
                COMBAT_ROOT + "Impact_BD_" + entry.Key + ".prefab" &&
            HasOnlyProjectileBehaviours(projectile) &&
            HasProjectileSorting(projectile) &&
            !HasForbiddenHealDependency(path);

        report.AppendLine(
            valid
                ? "[PASS] 전용 투사체"
                : "[FAIL] 전용 투사체");
        return valid ? 0 : 1;
    }

    private static bool HasOnlyProjectileBehaviours(GameObject projectile)
    {
        MonoBehaviour[] behaviours =
            projectile.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (!(behaviour is Projectile) &&
                !(behaviour is ProjectileVisual))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasProjectileSorting(GameObject projectile)
    {
        ParticleSystemRenderer[] renderers =
            projectile.GetComponentsInChildren<ParticleSystemRenderer>(true);
        if (renderers.Length == 0)
        {
            return false;
        }

        foreach (ParticleSystemRenderer renderer in renderers)
        {
            if (renderer.sortingLayerName != "Projectile")
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasForbiddenHealDependency(string projectilePath)
    {
        string[] dependencies = AssetDatabase.GetDependencies(projectilePath, true);
        foreach (string dependency in dependencies)
        {
            if (dependency.Contains("V2_17_heal"))
            {
                return true;
            }
        }
        return false;
    }

    private static void AppendAlignmentMetrics(
        StringBuilder report,
        string label,
        Color[] pixels,
        float radiusX)
    {
        const float VISIBLE_BRIGHTNESS = 0.03f;
        int minX = FRAME_SIZE;
        int maxX = -1;
        int minY = FRAME_SIZE;
        int maxY = -1;

        for (int y = 0; y < FRAME_SIZE; y++)
        {
            for (int x = 0; x < FRAME_SIZE; x++)
            {
                Color color = pixels[y * FRAME_SIZE + x];
                float brightness = Mathf.Max(color.r, color.g, color.b);
                if (brightness < VISIBLE_BRIGHTNESS)
                {
                    continue;
                }

                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }
        }

        if (maxX < minX || maxY < minY)
        {
            report.AppendLine($"[FAIL] {label} 렌더 경계 없음");
            return;
        }

        float pixelsPerWorldUnit = FRAME_SIZE / (ORTHO_SIZE * 2f);
        float expectedHalfWidth = radiusX * pixelsPerWorldUnit;
        float expectedHalfHeight =
            radiusX * IsometricMath.RADIUS_Y_RATIO * pixelsPerWorldUnit;
        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        float halfWidth = (maxX - minX + 1) * 0.5f;
        float halfHeight = (maxY - minY + 1) * 0.5f;

        report.AppendLine(
            $"[MEASURE] {label}: outer X={halfWidth / expectedHalfWidth:F3}, " +
            $"Y={halfHeight / expectedHalfHeight:F3}, " +
            $"center=({centerX - (FRAME_SIZE - 1) * 0.5f:F1}, " +
            $"{centerY - (FRAME_SIZE - 1) * 0.5f:F1})px");
    }

    private static Color[] Bake(
        GameObject prefab,
        float radius,
        Camera camera,
        RenderTexture target,
        Texture2D readback)
    {
        if (prefab == null)
        {
            return new Color[FRAME_SIZE * FRAME_SIZE];
        }

        GameObject host = Object.Instantiate(prefab);
        host.transform.position = Vector3.zero;
        host.transform.localScale = Vector3.one;

        Transform rangeScale = host.transform.Find(SCALE_OBJECT);
        if (rangeScale != null)
        {
            rangeScale.localScale *= radius;
        }

        foreach (Transform child in
            host.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = PREVIEW_LAYER;
        }

        foreach (ParticleSystem particles in
            host.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.useAutoRandomSeed = false;
            particles.randomSeed = FIXED_SEED;
            particles.Clear(false);
        }

        foreach (ParticleSystem particles in
            host.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Simulate(SAMPLE_TIME, false, true);
        }

        GameObject reference = CreateReferenceEllipse(radius);

        camera.backgroundColor = Color.black;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        readback.ReadPixels(
            new Rect(0, 0, FRAME_SIZE, FRAME_SIZE),
            0,
            0);
        readback.Apply();
        RenderTexture.active = previous;

        Color[] pixels = readback.GetPixels();
        Object.DestroyImmediate(reference.GetComponent<LineRenderer>().sharedMaterial);
        Object.DestroyImmediate(reference);
        Object.DestroyImmediate(host);
        return pixels;
    }

    private static GameObject CreateReferenceEllipse(float radiusX)
    {
        const int SEGMENTS = 128;
        const float LINE_WIDTH = 0.035f;
        var root = new GameObject("RangeIndicatorReference");
        root.layer = PREVIEW_LAYER;
        root.transform.position = new Vector3(0f, 0f, -0.5f);
        LineRenderer line = root.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = SEGMENTS;
        line.startWidth = LINE_WIDTH;
        line.endWidth = LINE_WIDTH;
        line.sortingOrder = 100;
        line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.magenta;
        line.endColor = Color.magenta;

        for (int i = 0; i < SEGMENTS; i++)
        {
            float angle = Mathf.PI * 2f * i / SEGMENTS;
            line.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radiusX,
                    Mathf.Sin(angle) * radiusX * IsometricMath.RADIUS_Y_RATIO,
                    0f));
        }

        return root;
    }

    private static GameObject CreateCamera(out Camera camera)
    {
        var host = new GameObject("BabyDragonVfxVerifyCamera");
        camera = host.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position =
            new Vector3(0f, 0f, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.cullingMask = 1 << PREVIEW_LAYER;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        return host;
    }
}
