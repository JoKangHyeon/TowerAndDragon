using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 암석 어미용 「메테오 방벽」의 타게팅 바닥 마커와 실제 설치 충격 프리팹을 만든다.
///
/// 타게팅 마커는 기존 암석 새끼용의 보라색 범위 마커를 그대로 쓰되 크기와 정렬만 맞춘다.
/// 설치 충격은 암석 타워 명중 이펙트를 베이스로 쓰고 기존 트레일 재질로 낙하 잔광을 더한다.
/// 외부 번들 원본(<c>PREVIEW_SOURCE_PATH</c> 등)은 읽기만 하고 수정하지 않는다.
///
/// 자동 실행은 하지 않는다 - 재생성은 Tools 메뉴에서 명시적으로 한다.
///
/// ⚠️ <b>마커의 파티클 설정(loop·emission·simulationSpace)은 손대지 않는다.</b> 한때 이것을
/// 1회성 Burst + World 공간으로 개조해 "타일이 바뀔 때마다 한 번 터지는 펄스"로 썼는데,
/// 겹침을 막으려 넣은 재생 간격 제한 때문에 마커가 커서를 느리게 따라왔다.
/// 지금은 <c>MeteorBarricadeSkill.UpdatePreviewMarker</c>가 인스턴스 하나를 매 프레임 옮기는
/// 방식이라, 원본의 루프 재생과 Local 공간이 그대로 필요하다.
///
/// 크기(<see cref="PREVIEW_SCALE"/>)는 루트 스케일로만 준다 - 원본이 scalingMode = Hierarchy라
/// 자식 파티클까지 따라 줄어든다.
/// </summary>
public static class BuildStoneBarricadeSkillVfx
{
    private const string PREVIEW_SOURCE_PATH =
        "Assets/Imported/Prefabs/BabyDragonEffect/Range/FX_BD_BuffRange_Stone.prefab";

    private const string IMPACT_SOURCE_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Stone.prefab";

    private const string TRAIL_MATERIAL_PATH =
        "Assets/Imported/Vefects/Anime VFX URP/Shared/Materials/M_VFX_Trail_03.mat";

    // 원본과 같은 번들 폴더에 둔다 - 외부 에셋을 가공한 파생본이라 원본 옆에 있어야 출처가 따라간다.
    // 스킬 에셋이 참조하는 프리팹도 여기 있으므로, 예전처럼 산출물과 참조가 어긋나지 않는다.
    private const string TARGET_FOLDER = "Assets/Imported/Prefabs/VFX/Dragon/Stone";

    // ⚠️ 이름이 TargetTilePulse에서 바뀌었다 - 1회성 펄스가 아니라 계속 떠 있는 마커다.
    // 새 파일이므로 GUID도 새로 발급된다. 스킬 에셋(SK_Dragon_Meteor)의
    // BarricadePreviewMoveVfxPrefab을 이쪽으로 다시 배선해야 한다.
    private const string PREVIEW_TARGET_PATH =
        TARGET_FOLDER + "/FX_StoneBarricade_TargetMarker.prefab";
    private const string IMPACT_TARGET_PATH =
        TARGET_FOLDER + "/FX_StoneBarricade_PlacementImpact.prefab";

    private const string PREVIEW_ROOT_NAME = "FX_StoneBarricade_TargetMarker";
    private const string IMPACT_ROOT_NAME = "FX_StoneBarricade_PlacementImpact";

    private const string HIGHLIGHT_SORTING_LAYER = "Highlight";
    private const string PROJECTILE_SORTING_LAYER = "Projectile";

    private const int PREVIEW_SORTING_ORDER = 10;
    private const int IMPACT_SORTING_ORDER = 5;
    private const int STREAK_SORTING_ORDER = 6;

    private const int IMPACT_MAX_PARTICLES = 96;

    private const float PREVIEW_SCALE = 0.72f;

    private const float IMPACT_SCALE_MULTIPLIER = 1.2f;
    private const float IMPACT_EMISSION_SECONDS = 0.65f;

    private const float STREAK_DURATION_SECONDS = 0.2f;
    private const float STREAK_LIFETIME_SECONDS = 0.22f;
    private const float STREAK_WIDTH = 0.24f;
    private const float STREAK_LENGTH = 3.2f;
    private const float STREAK_ANGLE_DEGREES = -18f;
    private const float STREAK_X = -0.42f;
    private const float STREAK_Y = 1.28f;
    private const float STREAK_Z = -0.05f;

    private const int QA_PANEL_SIZE = 420;
    private const int QA_DEPTH_BITS = 24;
    private const float QA_CAMERA_DISTANCE = 10f;
    private const float QA_CAMERA_ORTHO_SIZE = 2.4f;
    private const float QA_STAGE_Y = -100f;
    private const string QA_FILE_NAME = "TowerAndDragon_StoneBarricadeVfx_QA.png";

    private static readonly float[] QA_PREVIEW_TIMES = { 0.05f, 0.22f };
    private static readonly float[] QA_IMPACT_TIMES = { 0.05f, 0.22f, 0.5f };

    private static readonly Color STONE_VIOLET = new Color(0.69f, 0.49f, 0.878f, 0.9f);

    // ⚠️ 예전에는 [InitializeOnLoadMethod]로 "산출물이 없으면 자동 생성"을 걸어 뒀다. 걷어냈다.
    // 그 가드는 TARGET_FOLDER(아래)를 봤는데 실제 산출물은 다른 곳에 저장돼 있어 절대 충족되지 않았고,
    // 결과적으로 스크립트를 저장할 때마다·플레이 진입마다 도메인 리로드에서 2.5MB짜리 프리팹을
    // 새 GUID로 다시 만들고 AssetDatabase.SaveAssets()+Refresh()를 돌렸다. 아무도 참조하지 않는
    // 프리팹이 계속 쌓였고, 이 파일을 받은 팀원 전원에게 같은 일이 일어났다.
    // 재생성이 필요하면 아래 메뉴 항목으로 명시적으로 실행한다.

    [MenuItem("Tools/Tower and Dragons/Build Stone Barricade VFX")]
    private static void ExecuteFromMenu()
    {
        Debug.Log(Execute());
    }

    [MenuItem("Tools/Tower and Dragons/Render Stone Barricade VFX QA")]
    private static void RenderQaFromMenu()
    {
        Debug.Log(RenderQaSheet());
    }

    public static string Execute()
    {
        var report = new StringBuilder();

        EnsureFolder(TARGET_FOLDER);

        bool previewCreated = BuildPreview(report);
        bool impactCreated = BuildImpact(report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        report.AppendLine();
        report.Append("결과: 타게팅 ").Append(previewCreated ? "완료" : "실패")
            .Append(" / 설치 충격 ").AppendLine(impactCreated ? "완료" : "실패");

        return report.ToString();
    }

    public static string RenderQaSheet()
    {
        GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(PREVIEW_TARGET_PATH);
        GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_TARGET_PATH);

        if (preview == null || impact == null)
        {
            return "QA 렌더 실패: 생성 프리팹 두 개를 모두 찾지 못했습니다.";
        }

        int panelCount = QA_PREVIEW_TIMES.Length + QA_IMPACT_TIMES.Length;
        var sheet = new Texture2D(
            QA_PANEL_SIZE * panelCount,
            QA_PANEL_SIZE,
            TextureFormat.RGBA32,
            false);
        var renderTarget = new RenderTexture(
            QA_PANEL_SIZE,
            QA_PANEL_SIZE,
            QA_DEPTH_BITS,
            RenderTextureFormat.DefaultHDR);
        var cameraObject = new GameObject("StoneBarricadeVfxQaCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = QA_CAMERA_ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, QA_STAGE_Y, -QA_CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.065f, 0.09f, 1f);
        camera.allowHDR = true;
        camera.targetTexture = renderTarget;

        RenderTexture previous = RenderTexture.active;
        int panel = 0;

        try
        {
            foreach (float time in QA_PREVIEW_TIMES)
            {
                RenderPanel(preview, time, panel++, camera, renderTarget, sheet);
            }

            foreach (float time in QA_IMPACT_TIMES)
            {
                RenderPanel(impact, time, panel++, camera, renderTarget, sheet);
            }

            sheet.Apply(false, false);

            string outputPath = Path.Combine(Path.GetTempPath(), QA_FILE_NAME);
            File.WriteAllBytes(outputPath, sheet.EncodeToPNG());

            return "QA 렌더 저장: " + outputPath;
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            renderTarget.Release();
            Object.DestroyImmediate(renderTarget);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(sheet);
        }
    }

    private static void RenderPanel(
        GameObject prefab,
        float time,
        int panel,
        Camera camera,
        RenderTexture renderTarget,
        Texture2D sheet)
    {
        GameObject instance = Object.Instantiate(prefab);
        instance.transform.position = new Vector3(0f, QA_STAGE_Y, 0f);

        try
        {
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Simulate(time, false, true, true);
            }

            camera.Render();
            RenderTexture.active = renderTarget;

            var panelTexture = new Texture2D(
                QA_PANEL_SIZE,
                QA_PANEL_SIZE,
                TextureFormat.RGBA32,
                false);

            try
            {
                panelTexture.ReadPixels(
                    new Rect(0f, 0f, QA_PANEL_SIZE, QA_PANEL_SIZE),
                    0,
                    0,
                    false);
                panelTexture.Apply(false, false);
                sheet.SetPixels(
                    panel * QA_PANEL_SIZE,
                    0,
                    QA_PANEL_SIZE,
                    QA_PANEL_SIZE,
                    panelTexture.GetPixels());
            }
            finally
            {
                Object.DestroyImmediate(panelTexture);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static bool BuildPreview(StringBuilder report)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PREVIEW_SOURCE_PATH);

        if (source == null)
        {
            report.Append("타게팅 원본을 찾지 못했습니다: ").AppendLine(PREVIEW_SOURCE_PATH);
            return false;
        }

        GameObject instance = Object.Instantiate(source);
        instance.name = PREVIEW_ROOT_NAME;
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * PREVIEW_SCALE;

        try
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);

            // 정렬만 바꾼다. 파티클 모듈(loop·emission·simulationSpace·trails)은 원본 그대로 둔다 -
            // 이유는 클래스 주석 참고.
            foreach (ParticleSystem particles in systems)
            {
                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();

                if (renderer != null)
                {
                    renderer.sortingLayerName = HIGHLIGHT_SORTING_LAYER;
                    renderer.sortingOrder = PREVIEW_SORTING_ORDER;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(instance, PREVIEW_TARGET_PATH);

            report.AppendLine("[타게팅 마커]");
            report.Append("  원본: ").AppendLine(PREVIEW_SOURCE_PATH);
            report.Append("  저장: ").AppendLine(PREVIEW_TARGET_PATH);
            report.Append("  파티클 시스템: ").Append(systems.Length)
                .Append("개 / 원본 루프 재생 유지 / 크기 ").Append(PREVIEW_SCALE.ToString("0.##"))
                .AppendLine("배");

            return true;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static bool BuildImpact(StringBuilder report)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_SOURCE_PATH);
        Material trailMaterial = AssetDatabase.LoadAssetAtPath<Material>(TRAIL_MATERIAL_PATH);

        if (source == null)
        {
            report.Append("설치 충격 원본을 찾지 못했습니다: ").AppendLine(IMPACT_SOURCE_PATH);
            return false;
        }

        if (trailMaterial == null)
        {
            report.Append("트레일 재질을 찾지 못했습니다: ").AppendLine(TRAIL_MATERIAL_PATH);
            return false;
        }

        var root = new GameObject(IMPACT_ROOT_NAME);

        try
        {
            GameObject impact = Object.Instantiate(source, root.transform);
            impact.name = "GroundImpact";
            impact.transform.localPosition = Vector3.zero;
            impact.transform.localRotation = Quaternion.identity;
            impact.transform.localScale = source.transform.localScale * IMPACT_SCALE_MULTIPLIER;

            ParticleSystem[] impactSystems = impact.GetComponentsInChildren<ParticleSystem>(true);

            foreach (ParticleSystem particles in impactSystems)
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = false;
                main.playOnAwake = true;
                main.duration = Mathf.Min(main.duration, IMPACT_EMISSION_SECONDS);
                main.maxParticles = Mathf.Min(main.maxParticles, IMPACT_MAX_PARTICLES);
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();

                if (renderer != null)
                {
                    renderer.sortingLayerName = PROJECTILE_SORTING_LAYER;
                    renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, IMPACT_SORTING_ORDER);
                }
            }

            CreateMeteorStreak(root.transform, trailMaterial);

            PrefabUtility.SaveAsPrefabAsset(root, IMPACT_TARGET_PATH);

            report.AppendLine("[실제 설치]");
            report.Append("  충격 원본: ").AppendLine(IMPACT_SOURCE_PATH);
            report.Append("  트레일 재질: ").AppendLine(TRAIL_MATERIAL_PATH);
            report.Append("  저장: ").AppendLine(IMPACT_TARGET_PATH);
            report.Append("  원본 파티클 시스템: ").Append(impactSystems.Length)
                .AppendLine("개 + MeteorStreak");

            return true;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateMeteorStreak(Transform parent, Material material)
    {
        var streakObject = new GameObject("MeteorStreak");
        streakObject.transform.SetParent(parent, false);
        streakObject.transform.localPosition = new Vector3(STREAK_X, STREAK_Y, STREAK_Z);

        ParticleSystem particles = streakObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = true;
        main.duration = STREAK_DURATION_SECONDS;
        main.startLifetime = STREAK_LIFETIME_SECONDS;
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = STREAK_WIDTH;
        main.startSizeY = STREAK_LENGTH;
        main.startSizeZ = STREAK_WIDTH;
        main.startRotation = STREAK_ANGLE_DEGREES * Mathf.Deg2Rad;
        main.startColor = STONE_VIOLET;
        main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = BuildFadeGradient(STONE_VIOLET);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortingLayerName = PROJECTILE_SORTING_LAYER;
        renderer.sortingOrder = STREAK_SORTING_ORDER;
    }

    private static ParticleSystem.MinMaxGradient BuildFadeGradient(Color tint)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(tint, 0f),
                new GradientColorKey(tint, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(tint.a, 0.08f),
                new GradientAlphaKey(tint.a * 0.72f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });

        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void EnsureFolder(string path)
    {
        string[] segments = path.Split('/');
        string current = segments[0];

        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }
}
