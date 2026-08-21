using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 원본 이펙트 카탈로그는 보존하면서, 타워 전용으로 과한 방사형 파티클을 덜어낸 변형을 만든다.
public static class AssignTowerProjectiles
{
    private const string OUTPUT_FOLDER = "Assets/Imported/Prefabs/Projectile/Tower";
    private const string PROJECTILE_PROPERTY = "_projectilePrefab";
    private const string MUZZLE_PROPERTY = "_muzzlePrefab";
    private const string IMPACT_PROPERTY = "_impactPrefab";
    private const string MUZZLE_LIFETIME_PROPERTY = "_muzzleLifetimeSeconds";
    private const string IMPACT_LIFETIME_PROPERTY = "_impactLifetimeSeconds";
    private const string ROTATE_IMPACT_PROPERTY = "_rotateImpactToTravelDirection";

    private sealed class TowerEffectDefinition
    {
        public string TowerDataPath;
        public string OutputName;
        public string ProjectileSourcePath;
        public string ImpactSourcePath;
        public string MuzzleSourcePath;
        public string[] RemovedImpactChildren = Array.Empty<string>();
        public string[] RemovedMuzzleChildren = Array.Empty<string>();
        public string[] RemovedProjectileChildren = Array.Empty<string>();
        public float ProjectileScaleMultiplier = 1f;
        public float ImpactScaleMultiplier = 1f;
        public float MuzzleScaleMultiplier = 1f;
        public float MuzzleLifetimeSeconds = 0.2f;
        public float ImpactLifetimeSeconds = 0.35f;

        // 발사체·머즐·임팩트의 색조를 이 각도(0~360)로 돌린다. 음수면 원본 색 그대로.
        // 채도·명도·알파는 건드리지 않으므로 흰 심지는 희게, 검은 연기는 검게 남는다.
        // 머티리얼을 복제하지 않아도 되는 이유: 이 팩은 머티리얼 색이 전부 흰색이고
        // 실제 색은 파티클 startColor에서 나온다(계측으로 확인).
        public float HueDegrees = NO_HUE_OVERRIDE;

        // 발사체에만 다른 색조를 줄 때 쓴다. 음수면 HueDegrees를 그대로 따른다 -
        // 임팩트·머즐이 이미 원하는 색이고 발사체 원본만 색이 다를 때가 있다.
        public float ProjectileHueDegrees = NO_HUE_OVERRIDE;

        // 궤적 두께 배수. 발사체 클론의 <b>World 공간</b> 시스템에만 곱한다 -
        // 몸통은 Local, 뒤에 흘리는 궤적은 World라는 것이 이 팩의 일관된 규칙이다.
        public float TrailSizeMultiplier = 1f;

        // 명중 이펙트를 발사체가 날아온 각도로 통째로 돌릴지. 화살·총알처럼 튀는 방향이 있는
        // 임팩트에는 켜야 하지만, <b>지면 균열에는 꺼야 한다</b> - 켜 두면 아래로 쏜 발사체가
        // 맞았을 때 균열과 솟는 석영까지 뒤집혀 땅을 향한다.
        public bool RotatesImpactToTravelDirection = true;

        // World 규칙에 안 걸리지만 궤적으로 취급할 자식들. Vefects Fireball처럼 꼬리를 Local에
        // 붙여 둔 원본이 있어, 그런 경우에만 이름으로 집어 준다(다른 타워 동작은 바뀌지 않는다).
        public string[] ExtraTrailChildren = Array.Empty<string>();
    }

    private const float NO_HUE_OVERRIDE = -1f;
    private const string PROJECTILE_SORTING_LAYER = "Projectile";

    // 배정 원칙 1 — <b>날아가는 몸통이 보여야 한다.</b> 이 팩의 발사체 원본 중에는 몸통을 아예
    // 그리지 않는 것(ParticleSystemRenderer.renderMode = None)과, 늘어나는 빌보드(Stretch)를 쓰면서
    // 입자 속도가 0이라 길이가 0으로 접히는 것이 섞여 있다. 둘 다 화면에서는 발사체가 사라지고
    // 머즐과 임팩트만 남는다 - AAA_Vol2의 lightning·yellow·electro·nature·bloow·cuts가 전자다.
    // 후자는 우리 투사체가 트랜스폼만 옮기고 입자에는 속도를 주지 않기 때문에 생긴다(Projectile.cs).
    // 새 원본을 고를 때는 <b>Local 공간에 렌더되는 시스템</b>이 하나라도 있는지부터 확인할 것.
    //
    // 배정 원칙 2 — 임팩트는 짧고 작게. 크고 느리게 피어나면 타격이 아니라 폭죽으로 읽힌다.
    // 발사 간격이 짧은 타워일수록 더 줄인다. 유일한 예외는 광역인 암석 타워다.
    //
    // 원본을 전부 Vol1로 통일한 이유: Vol2는 프리팹 루트 스케일이 0.33이라 같은 배수를 줘도
    // 결과 크기가 3배 차이 난다. 한 팩으로 맞춰야 배수를 눈대중으로 비교할 수 있다.
    //
    // 스케일 배수는 "몸통 크기 × 루트 스케일"이 격자 한 칸(1)의 0.4~0.6이 되도록 잡았다.
    private static readonly TowerEffectDefinition[] DEFINITIONS =
    {
        // 0.75초 간격 연사. 임팩트가 조금만 길어도 화면에 겹쳐 남으므로 가장 짧게 잡는다.
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_Arrow.asset",
            OutputName = "Arrow",
            ProjectileSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Projectile_V1_01_nature_arrow.prefab",
            ImpactSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_01_nature_arrow.prefab",
            MuzzleSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_01_nature_arrow.prefab",
            RemovedImpactChildren = new[] { "SparksExplosion" },
            ProjectileScaleMultiplier = 1.3f,
            ImpactScaleMultiplier = 0.5f,
            HueDegrees = 210f,
            TrailSizeMultiplier = 3.5f,
            MuzzleLifetimeSeconds = 0.12f,
            ImpactLifetimeSeconds = 0.2f
        },
        // 화살보다 느리고 무겁다(1.25초·15). 같은 화살 계열이되 굵은 쪽을 쓰고 임팩트도 조금 크게.
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_CrossBow.asset",
            OutputName = "CrossBow",
            ProjectileSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Projectile_V1_21_red_arrow.prefab",
            ImpactSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_21_red_arrow.prefab",
            MuzzleSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_11_orange_arrow.prefab",
            RemovedImpactChildren = new[] { "Lightning", "SparksUp" },
            ProjectileScaleMultiplier = 1.2f,
            ImpactScaleMultiplier = 0.55f,
            MuzzleLifetimeSeconds = 0.15f,
            ImpactLifetimeSeconds = 0.26f
        },
        // 사거리 6.5의 저격. 총알 메시 + 짧은 잔상이 있는 원본을 쓴다 -
        // laser 계열은 Stretch뿐이라 우리 이동 방식에서는 길이가 접혀 아무것도 보이지 않는다.
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_Musket.asset",
            OutputName = "Musket",
            ProjectileSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Projectile_V1_14_blue_rapid.prefab",
            ImpactSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Impact_V1_14_blue_rapid.prefab",
            MuzzleSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_14_blue_rapid.prefab",
            RemovedImpactChildren = new[] { "BigSparks" },
            ProjectileScaleMultiplier = 1.5f,
            ImpactScaleMultiplier = 0.5f,
            MuzzleLifetimeSeconds = 0.16f,
            ImpactLifetimeSeconds = 0.22f
        },
        // 공중 전용. 요청에 따라 V2 lightning 세트로 되돌린 배선이다.
        //
        // <b>주의:</b> Projectile_V2_13_lightning은 ParticleSystemRenderer.renderMode가 None이라
        // 날아가는 몸통을 한 점도 그리지 않는다(계측 확인). 머즐과 임팩트만 보이고 그 사이는 비어 있다.
        // 몸통이 보여야 한다면 발사체만 Projectile_V1_02_electro로 바꾸면 된다 - 임팩트·머즐은 그대로 둘 수 있다.
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_Anti_Air.asset",
            OutputName = "AntiAir",
            ProjectileSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol2/Projectile_V2_13_lightning.prefab",
            ImpactSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol2/Impact/Impact_V2_03_electro.prefab",
            MuzzleSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol2/Muzzle/Muzzle_V2_13_lightning.prefab",
            RemovedImpactChildren = new[] { "Smoke", "SparksDonw" },
            ImpactScaleMultiplier = 0.6f,
            MuzzleLifetimeSeconds = 0.16f,
            ImpactLifetimeSeconds = 0.2f
        },
        // 화상을 남기는 타워. 발사체는 Vefects의 Fireball 전용 프리팹(불덩이 몸통 + 궤적만 담긴 Static 판),
        // 임팩트·머즐은 팀이 만들어 둔 Fire_V1을 쓴다.
        //
        // 원본은 Imported/ 아래라 손대지 않는다 - Projectile·ProjectileVisual은 클론에만 붙는다.
        // Distortion은 URP 왜곡 셰이더라 2D 렌더러에서 색 텍스처를 못 받으면 깨져 보이므로 뺀다.
        // 스케일 0.18: 원본은 사람 크기 기준이라 코어 Glow가 2.5였다. 격자 한 칸의 절반쯤으로 맞춘다.
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_FireTower.asset",
            OutputName = "Fire",
            ProjectileSourcePath = "Assets/Imported/Vefects/Anime VFX URP/Shared/Particles/VFX_Fireball_Projectile_Static.prefab",
            ImpactSourcePath = "Assets/Imported/Prefabs/Projectile/Impact_Fire_V1.prefab",
            MuzzleSourcePath = "Assets/Imported/Prefabs/Projectile/Muzzle_Fire_V1.prefab",
            RemovedProjectileChildren = new[] { "Fireball Projectile Distortion 01" },
            ProjectileScaleMultiplier = 0.18f,
            TrailSizeMultiplier = 2.5f,
            ExtraTrailChildren = new[]
            {
                "Fireball Projectile Trail 01", "Fireball Projectile Trail 02"
            },
            MuzzleLifetimeSeconds = 0.5f,
            ImpactLifetimeSeconds = 1f
        },
        // 둔화를 남기는 타워. 발사체는 결정 몸통과 눈가루 잔상이 함께 있는 Vol1 원본을 쓴다 -
        // Vol2의 ice는 몸통이 0.1에 불과해 날아가는 것이 보이지 않았다.
        //
        // 임팩트만은 이 팩이 아니라 BuildStatusVfx가 만든 얼음 균열을 쓴다
        // (Crack_Blue 바탕 + Bluerock의 석영 6개). 크기·방향이 이미 그 스크립트에서 맞춰져 있으므로
        // <b>여기서 배수를 주면 안 된다</b> - ImpactScaleMultiplier는 1로 둔다.
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_IceTower.asset",
            OutputName = "Ice",
            ProjectileSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Projectile_V1_26_blue_crystal.prefab",
            ImpactSourcePath = "Assets/Imported/Prefabs/Effects/Status/FX_Impact_IceCrack.prefab",
            MuzzleSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_26_blue_crystal.prefab",
            ProjectileScaleMultiplier = 1.4f,
            RotatesImpactToTravelDirection = false,
            MuzzleLifetimeSeconds = 0.16f,
            ImpactLifetimeSeconds = 1.5f
        },
        // 유일한 광역(반경 2)·최저속(5) 곡사.
        //
        // 임팩트는 BuildStatusVfx가 만든 암석 균열을 쓴다(Crack_Rock 바탕 + RockAOE의 광역 표시).
        // 얼음과 같은 이유로 여기서 배수를 주지 않고, 회전도 끈다 - 지면 균열이 발사각을 따라
        // 돌면 바닥이 뒤집힌다.
        // 출력 이름은 StoneMeteor 그대로 둔다 - 바꾸면 프리팹 GUID가 갈려 참조가 끊긴다.
        new TowerEffectDefinition
        {
            TowerDataPath = "Assets/Data/TowerData/TD_StoneTower.asset",
            OutputName = "StoneMeteor",
            ProjectileSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Projectile_V1_19_circle_bomb.prefab",
            ImpactSourcePath = "Assets/Imported/Prefabs/Effects/Status/FX_Impact_StoneCrack.prefab",
            MuzzleSourcePath = "Assets/Imported/Prefabs/Projectile/AAA_Vol1/Muzzle_V1_19_circle_bomb.prefab",
            ProjectileScaleMultiplier = 1.2f,
            HueDegrees = 280f,
            RotatesImpactToTravelDirection = false,
            MuzzleLifetimeSeconds = 0.2f,
            ImpactLifetimeSeconds = 1.4f
        }
    };

    public static string BuildAssignAndValidate()
    {
        EnsureOutputFolder();
        var report = new StringBuilder();

        foreach (TowerEffectDefinition definition in DEFINITIONS)
        {
            string impactPath = $"{OUTPUT_FOLDER}/Impact_Tower_{definition.OutputName}.prefab";
            string muzzlePath = $"{OUTPUT_FOLDER}/Muzzle_Tower_{definition.OutputName}.prefab";
            string projectilePath = $"{OUTPUT_FOLDER}/Projectile_Tower_{definition.OutputName}.prefab";

            CloneAndTrimEffect(
                definition.ImpactSourcePath,
                impactPath,
                $"Impact_Tower_{definition.OutputName}",
                definition.RemovedImpactChildren,
                definition.ImpactScaleMultiplier,
                definition.HueDegrees);

            // 머즐도 언제나 복제한다. 예전에는 지울 자식이 있을 때만 복제하고 아니면 원본을 그대로
            // 물렸는데, 그러면 MuzzleScaleMultiplier와 색조가 조용히 무시됐다(값을 넣어도 아무 일이
            // 없어 원인을 찾기 어렵다). 복제 비용은 프리팹 7개뿐이다.
            CloneAndTrimEffect(
                definition.MuzzleSourcePath,
                muzzlePath,
                $"Muzzle_Tower_{definition.OutputName}",
                definition.RemovedMuzzleChildren,
                definition.MuzzleScaleMultiplier,
                definition.HueDegrees);

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
            AssetDatabase.CreateFolder("Assets/Imported/Prefabs/Projectile", "Tower");
        }
    }

    private static void CloneAndTrimEffect(
        string sourcePath,
        string destinationPath,
        string outputName,
        IReadOnlyCollection<string> removedChildNames,
        float scaleMultiplier,
        float hueDegrees)
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
            ApplyHue(root, hueDegrees);
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
            RemoveNamedChildren(root, definition.RemovedProjectileChildren);
            root.transform.localScale *= definition.ProjectileScaleMultiplier;
            ApplyHue(
                root,
                definition.ProjectileHueDegrees >= 0f ? definition.ProjectileHueDegrees : definition.HueDegrees);
            ApplyTrailSize(root, definition.TrailSizeMultiplier, definition.ExtraTrailChildren);
            ForceSortingLayer(root);

            // 원본에 우리 컴포넌트가 없으면 <b>클론에</b> 붙인다. Imported/ 아래의 외부 번들은
            // 수정 금지라(CLAUDE.md) 원본에 붙일 수 없고, 붙일 필요도 없다 - 여기서 만드는 것은
            // 어차피 타워 전용 사본이다. ProjectileVisual은 Projectile을 RequireComponent로 끌고 온다.
            ProjectileVisual visual = root.GetComponent<ProjectileVisual>();
            if (visual == null)
            {
                visual = root.AddComponent<ProjectileVisual>();
            }

            GameObject muzzlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(muzzlePath);
            GameObject impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(impactPath);
            var serializedVisual = new SerializedObject(visual);
            serializedVisual.FindProperty(MUZZLE_PROPERTY).objectReferenceValue = muzzlePrefab;
            serializedVisual.FindProperty(IMPACT_PROPERTY).objectReferenceValue = impactPrefab;
            serializedVisual.FindProperty(MUZZLE_LIFETIME_PROPERTY).floatValue = definition.MuzzleLifetimeSeconds;
            serializedVisual.FindProperty(IMPACT_LIFETIME_PROPERTY).floatValue = definition.ImpactLifetimeSeconds;
            serializedVisual.FindProperty(ROTATE_IMPACT_PROPERTY).boolValue =
                definition.RotatesImpactToTravelDirection;
            serializedVisual.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, projectilePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 타워 이펙트는 전부 Projectile 정렬 레이어에 올린다. 다른 팩에서 가져온 원본은 Default에
    // 놓여 있어 그대로 두면 타일맵·건물에 가려지거나 반대로 UI처럼 위에 뜬다.
    // 레이어 안에서의 순서(sortingOrder)는 원본이 잡아 둔 앞뒤 관계이므로 건드리지 않는다.
    private static void ForceSortingLayer(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sortingLayerName = PROJECTILE_SORTING_LAYER;
        }
    }

    // 색조만 돌리고 채도·명도·알파는 그대로 둔다. 무채색(흰 심지, 검은 연기)은 채도가 0이라
    // 각도를 어떻게 주든 그대로 남으므로 예외 처리가 필요 없다.
    private static void ApplyHue(GameObject root, float hueDegrees)
    {
        if (hueDegrees < 0f)
        {
            return;
        }

        float hue = Mathf.Repeat(hueDegrees, 360f) / 360f;

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.startColor = ShiftHue(main.startColor, hue);
        }
    }

    private static ParticleSystem.MinMaxGradient ShiftHue(ParticleSystem.MinMaxGradient source, float hue)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(ShiftHue(source.color, hue));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    ShiftHue(source.colorMin, hue), ShiftHue(source.colorMax, hue));

            // 그라디언트 곡선까지 돌리려면 키를 하나씩 다시 써야 한다. 이 팩에는 없으므로 두고 본다.
            default:
                return source;
        }
    }

    private static Color ShiftHue(Color source, float hue)
    {
        Color.RGBToHSV(source, out float _, out float saturation, out float value);
        Color shifted = Color.HSVToRGB(hue, saturation, value);
        shifted.a = source.a;

        return shifted;
    }

    // 몸통은 Local, 궤적은 World. 궤적만 굵게 하려면 World 시스템의 입자 크기만 키우면 된다.
    // 그 규칙에서 벗어난 원본을 위해 이름으로도 집어 줄 수 있게 열어 둔다.
    private static void ApplyTrailSize(
        GameObject root, float multiplier, IReadOnlyCollection<string> extraTrailNames)
    {
        if (Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        HashSet<string> extraNames = extraTrailNames.ToHashSet();

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;

            bool isTrail =
                main.simulationSpace == ParticleSystemSimulationSpace.World ||
                extraNames.Contains(ps.gameObject.name);

            if (!isTrail)
            {
                continue;
            }

            main.startSize = new ParticleSystem.MinMaxCurve(main.startSize.constant * multiplier);
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
