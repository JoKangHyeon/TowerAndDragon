using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 화상 지속 연출의 시간축을 상태 지속시간(3초)에 맞춘다.
///
/// 문제: 원본은 포화까지 4초가 걸려, 화상이 끝나는 3초 시점에도 아직 커지는 중이었다.
/// 걸린 직후 1초가 가장 흐려서 "무엇이 걸렸는지"를 알려야 할 구간을 놓쳤다.
///
/// 원인이 두 겹이라 두 값을 함께 고친다.
/// - 누적: 연속 방출이라 입자가 쌓이는 데 시간이 걸린다 → 방출률 ×4
/// - 알파 곡선 (0,0)(0.29,0.93)(1,0): 각 입자가 수명의 29%가 지나야 최대 밝기다.
///   수명 5초면 1.45초 뒤, 1.25초면 0.36초 뒤 → 수명 ×0.25
///
/// 측정 근거(CoplayScripts/TuneStatusVfxLifetime): 0.15초 밝기 26%→41%,
/// 0.3~3초 변동폭 65%p→19%p.
///
/// 프리팹을 직접 고친다 - 작업노트 0장대로 프리팹이 결과물이고 생성기는 버려질 발판이다.
/// 되돌리려면: git -C Assets/Imported checkout Prefabs/Effects/Status/FX_Status_FireBurn.prefab
/// </summary>
public static class ApplyStatusVfxTiming
{
    private const string VFX_PATH =
        "Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn.prefab";

    private const float LIFETIME_SCALE = 0.25f;
    private const float RATE_SCALE = 4f;

    public static void Execute()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(VFX_PATH);

        if (root == null)
        {
            Debug.LogError($"[ApplyStatusVfxTiming] 프리팹을 열지 못했습니다: {VFX_PATH}");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine(
            $"[ApplyStatusVfxTiming] 수명 ×{LIFETIME_SCALE}, 방출 ×{RATE_SCALE}");

        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            ParticleSystem.EmissionModule emission = particles.emission;

            string before =
                $"lifetime={Describe(main.startLifetime)} rate={emission.rateOverTime.constant:0.#}";

            ScaleLifetime(particles);
            ScaleRate(particles);

            // duration은 loop=true면 방출 주기일 뿐이지만, 수명보다 훨씬 길면 편집기에서
            // 타임라인이 읽기 어려워진다 - 수명에 맞춰 같이 줄인다.
            main = particles.main;
            main.duration = Mathf.Max(main.duration * LIFETIME_SCALE, MIN_DURATION);

            emission = particles.emission;

            report.AppendLine(
                $"  {particles.name}: {before} → " +
                $"lifetime={Describe(main.startLifetime)} " +
                $"rate={emission.rateOverTime.constant:0.#} duration={main.duration:0.##}");
        }

        PrefabUtility.SaveAsPrefabAsset(root, VFX_PATH);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.Refresh();

        Debug.Log(report.ToString());
    }

    private const float MIN_DURATION = 1f;

    private static void ScaleLifetime(ParticleSystem particles)
    {
        ParticleSystem.MainModule main = particles.main;
        ParticleSystem.MinMaxCurve lifetime = main.startLifetime;

        if (lifetime.mode == ParticleSystemCurveMode.TwoConstants)
        {
            lifetime.constantMin *= LIFETIME_SCALE;
            lifetime.constantMax *= LIFETIME_SCALE;
        }
        else
        {
            lifetime.constant *= LIFETIME_SCALE;
        }

        main.startLifetime = lifetime;
    }

    private static void ScaleRate(ParticleSystem particles)
    {
        ParticleSystem.EmissionModule emission = particles.emission;
        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;

        // constant는 constantMax의 별칭이다 - 둘 다 곱하면 배수가 제곱으로 걸린다
        // (13이 4배가 아니라 16배인 208이 됐다). 모드에 맞는 쪽만 건드린다.
        if (rate.mode == ParticleSystemCurveMode.TwoConstants)
        {
            rate.constantMin *= RATE_SCALE;
            rate.constantMax *= RATE_SCALE;
        }
        else
        {
            rate.constant *= RATE_SCALE;
        }

        emission.rateOverTime = rate;
    }

    private static string Describe(ParticleSystem.MinMaxCurve curve)
    {
        if (curve.mode == ParticleSystemCurveMode.TwoConstants)
        {
            return $"{curve.constantMin:0.##}~{curve.constantMax:0.##}";
        }

        return $"{curve.constant:0.##}";
    }
}
