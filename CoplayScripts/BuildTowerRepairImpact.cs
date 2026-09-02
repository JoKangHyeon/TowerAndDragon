using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 시간 어미용 「즉시 재활성화」의 대상별 연출(B계층)을 만든다.
///
/// 생명 「성벽 재생」(<see cref="BuildCastleHealImpact"/>)과 <b>같은 그림, 색만 노랑</b>이다 -
/// 둘 다 <c>Impact_Tower_Life</c>의 화살표 버스트를 쓰고, 크기와 색조만 다르다.
/// 「무언가가 되살아난다」는 신호를 두 속성이 공유하되 색으로 갈리는 형태다.
///
/// 크기: 대상이 성이 아니라 <b>타워</b>다. 타워 스프라이트는 실제 0.88~1.6 월드 단위로
/// (측정: CoplayScripts/MeasureTowerPrefabSize) 성(3.2)의 1/3~1/2이라 성벽 재생본보다 작게 만든다.
///
/// 색: 설계 §6-1의 시간 색조 <b>42도</b>(황금빛). 원본은 초록(생명)이라 색조만 돌린다 -
/// 채도·명도는 건드리지 않는다. 암석 오버레이에서 채도를 안 눌러 핫핑크가 됐던 것과 달리,
/// 초록 → 노랑은 같은 난색·유채색 계열이라 채도를 그대로 옮겨도 어색하지 않다.
///
/// <b>원본을 고치지 않는다</b> - <c>Projectile_Tower_Life</c>가 참조하는 생명 타워 명중 이펙트다.
///
/// 되돌리려면: git -C Assets/Imported checkout Prefabs/SkillCastOverlay
/// </summary>
public static class BuildTowerRepairImpact
{
    private const string SOURCE_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Life.prefab";

    private const string TARGET_PATH =
        "Assets/Imported/Prefabs/SkillCastOverlay/FX_TowerRepairImpact.prefab";

    // 타워 스프라이트의 실제 폭(측정값). 프리팹에 따라 0.88~1.6이라 큰 쪽에 맞춘다 -
    // 작은 타워에서 조금 넘치는 것이 큰 타워에서 모자란 것보다 낫다.
    private const float TOWER_WIDTH = 1.6f;

    // 원본 프리팹의 루트 스케일. 타워 명중 기준이다.
    private const float SOURCE_SCALE = 0.38f;

    // 타워를 감싸되 옆 타워까지 덮지 않을 정도. 성벽 재생본(1.15)보다 여유를 조금 더 준다 -
    // 대상이 작아 같은 비율이면 존재감이 약하다.
    private const float COVERAGE_RATIO = 1.3f;

    // 설계 §6-1의 시간 색조(황금빛).
    private const float TIME_HUE_DEGREES = 42f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_PATH);

        if (source == null)
        {
            return "원본을 찾지 못했습니다: " + SOURCE_PATH;
        }

        var report = new StringBuilder();

        GameObject instance = Object.Instantiate(source);
        instance.name = System.IO.Path.GetFileNameWithoutExtension(TARGET_PATH);

        try
        {
            float scale = SOURCE_SCALE * TOWER_WIDTH * COVERAGE_RATIO;

            instance.transform.localScale = new Vector3(scale, scale, scale);

            report.Append("루트 스케일: ").Append(SOURCE_SCALE.ToString("0.##"))
                .Append(" → ").AppendLine(scale.ToString("0.##"));
            report.Append("색조 → ").Append(TIME_HUE_DEGREES).AppendLine("도 (황금빛)");
            report.AppendLine();

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;

                // 루트 스케일이 파티클에 곱해져야 한다. Local이면 배치만 벌어지고 그림이 안 커진다.
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                ParticleSystem.MinMaxGradient before = main.startColor;
                main.startColor = ShiftHue(before);

                var overLifetime = particles.colorOverLifetime;

                if (overLifetime.enabled)
                {
                    overLifetime.color = ShiftHue(overLifetime.color);
                }

                report.Append("  ").Append(particles.name.PadRight(14))
                    .Append(Summarize(before)).Append(" → ")
                    .AppendLine(Summarize(main.startColor));
            }

            PrefabUtility.SaveAsPrefabAsset(instance, TARGET_PATH);
            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.Append("저장: ").AppendLine(TARGET_PATH);
            report.Append("원본(수정 안 함): ").AppendLine(SOURCE_PATH);
            report.Append("타워 폭 ").Append(TOWER_WIDTH.ToString("0.##"))
                .Append(" × 여유 ").Append(COVERAGE_RATIO.ToString("0.##")).AppendLine("배 기준");

            return report.ToString();
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static ParticleSystem.MinMaxGradient ShiftHue(ParticleSystem.MinMaxGradient source)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(Shift(source.color));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    Shift(source.colorMin), Shift(source.colorMax));

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(Shift(source.gradient));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    Shift(source.gradientMin), Shift(source.gradientMax));

            default:
                return source;
        }
    }

    // 채도가 0에 가까운 색(흰 섬광 등)은 건드리지 않는다 - 색조를 돌려도 흰색은 흰색이지만,
    // 굳이 계산해 미세한 색이 끼는 것을 막는다.
    private static Color Shift(Color source)
    {
        Color.RGBToHSV(source, out float _, out float saturation, out float value);

        if (saturation <= 0.01f)
        {
            return source;
        }

        Color shifted = Color.HSVToRGB(
            Mathf.Repeat(TIME_HUE_DEGREES, 360f) / 360f, saturation, value);

        shifted.a = source.a;

        return shifted;
    }

    private static Gradient Shift(Gradient source)
    {
        if (source == null)
        {
            return null;
        }

        GradientColorKey[] colorKeys = source.colorKeys;

        for (int i = 0; i < colorKeys.Length; i++)
        {
            colorKeys[i].color = Shift(colorKeys[i].color);
        }

        var shifted = new Gradient();
        shifted.mode = source.mode;
        shifted.SetKeys(colorKeys, source.alphaKeys);

        return shifted;
    }

    private static string Summarize(ParticleSystem.MinMaxGradient gradient)
    {
        switch (gradient.mode)
        {
            case ParticleSystemGradientMode.Color:
                return Describe(gradient.color);

            case ParticleSystemGradientMode.TwoColors:
                return Describe(gradient.colorMin) + "~" + Describe(gradient.colorMax);

            default:
                return gradient.mode.ToString();
        }
    }

    private static string Describe(Color color)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out float _);

        return $"h{(hue * 360f):0}s{(saturation * 100f):0}";
    }
}
