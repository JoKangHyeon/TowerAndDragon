using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 버프 타워 4종의 범위 마커를 새끼용 버프 범위와 같은 소스·같은 처리로 다시 굽는다.
/// Coplay execute_script 전용 편집기 스크립트다.
///
/// 이전 판(BuildAuraMarkers)은 "Marker circle fantasy/sci-fi"에 맥동 평탄화(수명 x12, 주기 x3)를
/// 걸어 만들었는데, 그 처리가 밝기를 희석시켜 화면에서 너무 흐렸다. 새끼용 쪽은 원본 맥동을
/// 그대로 두고 ColorOverLifetime만 살려 두므로 같은 방식으로 맞춘다.
///
/// 저장 경로는 기존 프리팹과 같다 - GUID가 유지되므로 오라 에셋 4장의 배선을 다시 하지 않는다.
/// </summary>
public static class RebuildAuraMarkers
{
    private const string SOURCE_ROOT =
        "Assets/Imported/Hovl Studio_waypoints/Way points/Prefabs/Loop version/";
    private const string OUTPUT_ROOT = "Assets/Imported/Prefabs/Aura/";
    private const string REPORT_PATH = "output/vfx_check/aura_marker_rebuild.txt";

    private const string SCALE_OBJECT = "Scale";
    private const string RANGE_RING_PARTICLE_NAME = "Circle";

    // 새끼용 버프 범위와 같은 값이다(BuildBabyDragonRangeVfx). 판정선의 95%에서 링이 끝나고,
    // y를 절반으로 눌러 아이소 평면에 눕힌다.
    private const float NORMALIZED_XZ_SCALE = 0.95f;
    private const float ISOMETRIC_Y_SCALE = 0.475f;

    // 맥동 주기는 박지 않는다 - 새끼용 버프 범위 프리팹에서 그대로 읽어 온다.
    // 그쪽은 SyncBabyDragonRangePulse가 속성별 검 마커의 실제 반복 주기를 링에 넣어 뒀고,
    // 값이 속성마다 다르다. 여기에 숫자를 박으면 그 값이 바뀔 때 두 표시가 어긋난다.
    private const string BABY_DRAGON_RANGE_ROOT =
        "Assets/Imported/Prefabs/BabyDragon/Range/";
    private const float FALLBACK_PULSE_SECONDS = 1f;

    private const int SORTING_LAYER_ID = 0;
    private const int SORTING_ORDER = 10;

    private struct Entry
    {
        public string Key;
        public string SourceColor;
        public DragonType Palette;
    }

    // 색은 DragonAttributePalette에서 가져온다 - 새끼용과 같은 색표를 쓰므로 두 표시가 화면에서
    // 같은 언어로 읽힌다. 속성 대응은 색만 빌리는 것이고 게임 규칙과는 무관하다.
    private static readonly Entry[] ENTRIES =
    {
        new Entry { Key = "Time", SourceColor = "yellow", Palette = DragonType.Time },
        new Entry { Key = "Stealth", SourceColor = "violet", Palette = DragonType.Stone },
        new Entry { Key = "Soul", SourceColor = "cyan", Palette = DragonType.Ice },
        new Entry { Key = "Enhancement", SourceColor = "orange", Palette = DragonType.Fire },
    };

    public static void Execute()
    {
        Directory.CreateDirectory("output/vfx_check");

        var report = new StringBuilder();
        report.AppendLine("[RebuildAuraMarkers]");
        report.AppendLine(
            $"  Scale ({NORMALIZED_XZ_SCALE}, {ISOMETRIC_Y_SCALE}, {NORMALIZED_XZ_SCALE})" +
            $" / 정렬 Default/{SORTING_ORDER} / 맥동은 새끼용 버프 범위에서 읽는다");
        report.AppendLine();

        foreach (Entry entry in ENTRIES)
        {
            string sourcePath =
                SOURCE_ROOT + "Marker circle simple " + entry.SourceColor + ".prefab";
            string outputPath = OUTPUT_ROOT + "FX_Aura_" + entry.Key + ".prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

            if (source == null)
            {
                report.AppendLine($"[없음] {sourcePath}");
                continue;
            }

            Color tint = DragonAttributePalette.ColorOf(entry.Palette);
            float pulseSeconds = ReadBabyDragonPulseSeconds(entry.Palette);
            GameObject root = Compose("FX_Aura_" + entry.Key, source);
            CorrectParticles(root, tint);
            SynchronizeRangeRingPulse(root, pulseSeconds);
            PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            Object.DestroyImmediate(root);

            report.AppendLine(
                $"  {entry.Key,-12} <- simple {entry.SourceColor,-6}" +
                $" 틴트 {ColorText(tint)} 맥동 {pulseSeconds:F3}초" +
                $" (새끼용 {entry.Palette}) / {outputPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        File.WriteAllText(REPORT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    private static GameObject Compose(string name, GameObject source)
    {
        var root = new GameObject(name);
        var scale = new GameObject(SCALE_OBJECT);
        scale.transform.SetParent(root.transform, false);
        scale.transform.localScale = new Vector3(
            NORMALIZED_XZ_SCALE,
            ISOMETRIC_Y_SCALE,
            NORMALIZED_XZ_SCALE);

        GameObject body = Object.Instantiate(source, scale.transform);
        body.name = source.name;
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one;

        return root;
    }

    private static void CorrectParticles(GameObject root, Color tint)
    {
        foreach (ParticleSystem particles in
            root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            // Mesh 렌더 + startRotationX 90도는 회전 0인 정사영 카메라에서 한 픽셀도 안 그려진다.
            main.startRotationX = 0f;

            bool isRangeRing = particles.name == RANGE_RING_PARTICLE_NAME;

            // 범위 형태는 첫 프레임부터 최종 크기로 고정한다. 맥동은 원본 ColorOverLifetime만
            // 남겨 밝기 변화로 표현한다.
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = false;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = isRangeRing;

            ApplyTint(particles, tint);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();

            if (renderer != null)
            {
                renderer.enabled = isRangeRing;
                renderer.sortingLayerID = SORTING_LAYER_ID;
                renderer.sortingOrder = SORTING_ORDER;
            }
        }
    }

    private static void SynchronizeRangeRingPulse(
        GameObject root, float pulseSeconds)
    {
        Transform circle = FindChild(root.transform, RANGE_RING_PARTICLE_NAME);
        ParticleSystem particles =
            circle != null ? circle.GetComponent<ParticleSystem>() : null;

        if (particles == null)
        {
            return;
        }

        ParticleSystem.MainModule main = particles.main;
        main.duration = pulseSeconds;
        main.startLifetime = pulseSeconds;
    }

    // 대응 속성의 새끼용 버프 범위 링에서 주기를 읽는다. 그쪽이 바뀌면 여기도 따라가게 하려는 것이다.
    private static float ReadBabyDragonPulseSeconds(DragonType attribute)
    {
        string path =
            BABY_DRAGON_RANGE_ROOT + "FX_BD_BuffRange_" + attribute + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            return FALLBACK_PULSE_SECONDS;
        }

        Transform circle = FindChild(prefab.transform, RANGE_RING_PARTICLE_NAME);
        ParticleSystem particles =
            circle != null ? circle.GetComponent<ParticleSystem>() : null;

        return particles != null
            ? particles.main.duration
            : FALLBACK_PULSE_SECONDS;
    }

    private static void ApplyTint(ParticleSystem particles, Color tint)
    {
        ParticleSystem.MainModule main = particles.main;
        main.startColor = Recolor(main.startColor, tint);

        ParticleSystem.ColorOverLifetimeModule lifetime = particles.colorOverLifetime;

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

    private static string ColorText(Color color) =>
        $"({color.r:0.00}, {color.g:0.00}, {color.b:0.00})";
}
