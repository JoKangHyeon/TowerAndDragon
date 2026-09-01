using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 암석 시전 오버레이를 <c>Side fumes</c> 기반으로 다시 만든다.
///
/// 왜 바꾸나: 기존 것은 설계 §3-2가 1순위로 지정한 <c>Side highlights violet</c> 파생본인데,
/// 실물이 <c>Debris1</c>·<c>Glow1</c>(반짝이는 작은 입자) 머티리얼이라 <b>암석 속성에 비해 너무
/// 아기자기하다</b>는 플레이 테스트 판단이 나왔다. <c>Side fumes</c>는 SmokeFlow·SmokeSide·SkyFog·
/// Smoke·Debris 구성이라 연기와 잔해로 무게감이 있다.
///
/// 설계 §3-2의 변환 3개를 M2와 같은 값으로 적용한다:
///   ① maxNumParticles 1000/2000 → 128 (경량화)
///   ② scalingMode Hierarchy → Local (카메라 자식일 때 부모 스케일이 곱해지는 함정 회피)
///   ③ 소팅 Default → Highlight:-100 (구름=Fog보다 위, 마커보다 아래 - M2 참고)
/// looping은 1로 둔다 - 호스트가 StopEmitting + 알파 페이드로 끝낸다.
///
/// 색조는 보라(271도)로 돌린다 - 설계 §6-1의 암석 색상이다. Side fumes 원본은 주황·회색이다.
/// 알파는 다른 4속성과 같은 기준(원본의 0.5배)으로 낮춘다.
///
/// <b>원본을 고치지 않는다.</b> Assets/Imported의 Hovl 팩은 수정 금지 원본이라 파생본을 만든다.
/// 기존 FX_DragonSkillCastOverlay_Stone.prefab을 덮어써서 배선(호스트의 _overlays)을 유지한다.
///
/// 되돌리려면: git -C Assets/Imported checkout Prefabs/SkillCastOverlay
/// </summary>
public static class RebuildStoneOverlay
{
    private const string SOURCE_PATH =
        "Assets/Imported/Hovl Studio_background/Background VFX/Prefabs/Side fumes.prefab";

    private const string TARGET_PATH =
        "Assets/Imported/Prefabs/SkillCastOverlay/FX_DragonSkillCastOverlay_Stone.prefab";

    private const string SORTING_LAYER = "Highlight";
    private const int SORTING_ORDER = -100;

    private const int MAX_PARTICLES = 128;

    // 설계 §6-1의 암석 색조.
    private const float STONE_HUE_DEGREES = 271f;

    // ⚠️ 색조만 돌리고 채도를 그대로 두면 <b>핫핑크</b>가 된다(실제로 그렇게 나왔다 - 암석이 아니라
    //   벚꽃처럼 보였다). Side fumes 원본은 채도 높은 주황 불꽃이라, 271도로 돌리면 그 채도가
    //   그대로 자홍으로 옮겨간다. 암석은 <b>채도가 낮은 회보라</b>여야 하므로 함께 눌러 준다.
    private const float SATURATION_SCALE = 0.35f;

    // 채도를 낮추면 전체가 뿌옇게 밝아진다. 명도를 함께 낮춰 돌·먼지의 무게를 남긴다.
    // 0.75는 아직 흰 연기로 보였다 - 더 눌러 회보라 쪽으로 가져간다.
    private const float VALUE_SCALE = 0.5f;

    // 알파 1배(원본)로 두니 흰 연기가 두꺼워 성이 가려졌다. 0.5배는 반대로 사라졌다 -
    // 그 사이를 잡는다. 배경이지 안개가 아니므로 월드가 비쳐 보여야 한다.
    private const float ALPHA_SCALE = 0.55f;

    // 화면(가로 17.8 × 세로 10 단위)보다 넓게 깔아야 가장자리가 비지 않는다 - M2에서 불이
    // 방출기가 화면 밖으로 밀려 좌하단만 덮었던 것과 같은 문제를 미리 막는다.
    private const float FRAME_WIDTH = 22f;

    // ⚠️ Side fumes 원본은 방출기가 y 7.9~12.7에 흩어져 있다. 이 프로젝트 화면은 세로 ±5라
    //   그대로 두면 대부분이 화면 위쪽 밖에서 뿜어 <b>거의 안 보인다</b>(실제로 그렇게 나왔다).
    //   화면 안으로 끌어내리되 원래의 상하 배치 순서는 유지하도록 배율로 누른다.
    private const float VERTICAL_COMPRESSION = 0.35f;

    // 압축 후에도 이 범위를 넘으면 잘라낸다. 화면 세로 절반(5)보다 살짝 안쪽이다.
    private const float MAX_ABS_Y = 4f;

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
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;

                int beforeMax = main.maxParticles;
                main.maxParticles = MAX_PARTICLES;

                // 카메라 자식으로 붙으므로 부모 스케일이 곱해지면 안 된다.
                main.scalingMode = ParticleSystemScalingMode.Local;

                // 호스트가 끝을 제어한다 - 루프인 채로 둔다.
                main.loop = true;

                main.startColor = ShiftAndFade(main.startColor);

                var overLifetime = particles.colorOverLifetime;

                if (overLifetime.enabled)
                {
                    overLifetime.color = ShiftAndFade(overLifetime.color);
                }

                // 방출 범위를 화면보다 넓게. 이미 넓은 것은 건드리지 않는다.
                ParticleSystem.ShapeModule shape = particles.shape;
                Vector3 shapeScale = shape.scale;

                if (shapeScale.x > 0f && shapeScale.x < FRAME_WIDTH)
                {
                    shapeScale.x = FRAME_WIDTH;
                    shape.scale = shapeScale;
                }

                // 방출기가 화면 밖에 있으면 안 보인다 - 가로는 중앙으로, 세로는 화면 안으로 누른다.
                Vector3 local = particles.transform.localPosition;
                local.x = 0f;
                local.y = Mathf.Clamp(local.y * VERTICAL_COMPRESSION, -MAX_ABS_Y, MAX_ABS_Y);
                particles.transform.localPosition = local;

                var renderer = particles.GetComponent<ParticleSystemRenderer>();

                if (renderer != null)
                {
                    renderer.sortingLayerName = SORTING_LAYER;
                    renderer.sortingOrder = SORTING_ORDER;
                }

                report.Append("  ").Append(particles.name.PadRight(14))
                    .Append(" max ").Append(beforeMax).Append("→").Append(MAX_PARTICLES)
                    .Append("  shapeX ").Append(shape.scale.x.ToString("0.#"))
                    .Append("  pos ").AppendLine(particles.transform.localPosition.ToString("0.##"));
            }

            PrefabUtility.SaveAsPrefabAsset(instance, TARGET_PATH);
            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.Append("저장: ").AppendLine(TARGET_PATH);
            report.Append("원본: ").AppendLine(SOURCE_PATH);
            report.Append("색조 ").Append(STONE_HUE_DEGREES).Append("도 / 알파 ")
                .Append(ALPHA_SCALE.ToString("0.##")).Append("배 / 소팅 ")
                .Append(SORTING_LAYER).Append(':').AppendLine(SORTING_ORDER.ToString());

            return report.ToString();
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static ParticleSystem.MinMaxGradient ShiftAndFade(ParticleSystem.MinMaxGradient source)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(Convert(source.color));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(
                    Convert(source.colorMin), Convert(source.colorMax));

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(Convert(source.gradient));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(
                    Convert(source.gradientMin), Convert(source.gradientMax));

            default:
                return source;
        }
    }

    private static Color Convert(Color source)
    {
        Color.RGBToHSV(source, out float _, out float saturation, out float value);

        Color shifted = Color.HSVToRGB(
            Mathf.Repeat(STONE_HUE_DEGREES, 360f) / 360f,
            Mathf.Clamp01(saturation * SATURATION_SCALE),
            Mathf.Clamp01(value * VALUE_SCALE));

        shifted.a = Mathf.Clamp01(source.a * ALPHA_SCALE);

        return shifted;
    }

    private static Gradient Convert(Gradient source)
    {
        if (source == null)
        {
            return null;
        }

        GradientColorKey[] colorKeys = source.colorKeys;

        for (int i = 0; i < colorKeys.Length; i++)
        {
            colorKeys[i].color = Convert(colorKeys[i].color);
        }

        GradientAlphaKey[] alphaKeys = source.alphaKeys;

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha = Mathf.Clamp01(alphaKeys[i].alpha * ALPHA_SCALE);
        }

        var converted = new Gradient();
        converted.mode = source.mode;
        converted.SetKeys(colorKeys, alphaKeys);

        return converted;
    }
}
