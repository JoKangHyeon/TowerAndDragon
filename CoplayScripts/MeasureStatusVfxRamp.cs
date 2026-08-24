using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태 지속 연출이 "달아오르는" 데 걸리는 시간을 잰다.
///
/// 화상 지속은 3초인데 이 연출은 처음에 작은 불씨로 시작해 점점 진해지고 솟는다.
/// 그 상승 구간이 3초보다 길면, 제대로 보일 때쯤 상태가 끝나 무엇이 걸렸는지 읽히지 않는다.
///
/// 재는 것은 두 가지다.
/// - 시간대별 살아 있는 입자 수(얼마나 차오르는가)
/// - 방출·수명·상승 관련 실제 설정값(무엇을 줄여야 하는가)
/// </summary>
public static class MeasureStatusVfxRamp
{
    private const string VFX_PATH =
        "Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn.prefab";

    private const float BURN_DURATION = 3f;
    private const float SIMULATE_STEP = 0.25f;
    private const float SIMULATE_END = 6f;

    private const string OUTPUT_PATH = "output/vfx_check/status_vfx_ramp.txt";

    public static void Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_PATH);

        if (prefab == null)
        {
            Debug.LogError($"[MeasureStatusVfxRamp] 프리팹이 없습니다: {VFX_PATH}");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine($"[MeasureStatusVfxRamp] {prefab.name} (화상 지속 {BURN_DURATION}초)");
        report.AppendLine();

        // 1) 설정값 먼저.
        report.AppendLine("설정:");

        foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;
            ParticleSystem.EmissionModule emission = particles.emission;

            float burst = 0f;

            for (int i = 0; i < emission.burstCount; i++)
            {
                burst += emission.GetBurst(i).count.constant;
            }

            report.AppendLine(
                $"  {particles.name}: lifetime={Describe(main.startLifetime)} " +
                $"rate={emission.rateOverTime.constant:0.#} burst={burst:0.#} " +
                $"startSpeed={Describe(main.startSpeed)} " +
                $"startSize={Describe(main.startSize)} " +
                $"duration={main.duration:0.##} loop={main.loop}");
        }

        // 2) 시간대별 입자 수.
        GameObject instance = Object.Instantiate(prefab);
        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);

        report.AppendLine();
        report.AppendLine("시간별 살아 있는 입자 수:");

        foreach (ParticleSystem particles in systems)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Clear(true);
        }

        var header = new StringBuilder("  t(초) ");

        foreach (ParticleSystem particles in systems)
        {
            header.Append($"| {particles.name} ");
        }

        header.Append("| 합계");
        report.AppendLine(header.ToString());

        ParticleSystem root = instance.GetComponent<ParticleSystem>();

        for (float t = SIMULATE_STEP; t <= SIMULATE_END + 0.001f; t += SIMULATE_STEP)
        {
            // 루트에서 한 번만 Simulate하면 withChildren=true로 자식까지 같은 시간축을 탄다.
            // 매번 처음부터 다시 굽는다(restart=true) - 누적 오차를 피한다.
            if (root != null)
            {
                root.Simulate(t, true, true);
            }
            else
            {
                foreach (ParticleSystem particles in systems)
                {
                    particles.Simulate(t, true, true);
                }
            }

            var line = new StringBuilder($"  {t:0.00} ");
            int total = 0;

            foreach (ParticleSystem particles in systems)
            {
                int count = particles.particleCount;
                total += count;
                line.Append($"| {count} ");
            }

            line.Append($"| {total}");

            // 화상이 끝나는 지점을 눈에 띄게 표시한다.
            if (Mathf.Abs(t - BURN_DURATION) < 0.001f)
            {
                line.Append("   ← 화상 종료");
            }

            report.AppendLine(line.ToString());
        }

        Object.DestroyImmediate(instance);

        // 플레이 중이면 전투 로그가 콘솔을 밀어내 결과를 놓친다 - 파일로도 남긴다.
        System.IO.File.WriteAllText(OUTPUT_PATH, report.ToString());

        Debug.Log(report.ToString());
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
