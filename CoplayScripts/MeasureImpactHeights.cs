using System.Text;
using UnityEditor;
using UnityEngine;

// 얼음과 암석 명중 이펙트가 화면에서 어느 높이에 그려지는지 잰다.
//
// 둘 다 "지면에서 솟는" 그림이라 밑동이 같은 높이에 있어야 한다. 눈대중으로는 못 맞추므로
// 시뮬레이트한 뒤 렌더러 경계를 읽어 숫자로 비교한다.
//
// 기준점(스폰 위치)을 y=0으로 두고 잰다. 음수면 스폰 지점보다 아래, 양수면 위다.
public static class MeasureImpactHeights
{
    private static readonly string[] IMPACT_PATHS =
    {
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab",
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab"
    };

    private static readonly float[] TIMES = { 0.1f, 0.2f, 0.35f };

    private const float STAGE_Y = -200f;

    // 이보다 얇은 것은 아직 입자가 없는 셈 치고 뺀다. 빈 시스템의 경계는 원점에 붙어 있어
    // 그대로 두면 밑동이 실제보다 낮게 나온다.
    private const float EMPTY_BOUNDS_EPSILON = 0.001f;

    public static string Execute()
    {
        var report = new StringBuilder();

        foreach (string path in IMPACT_PATHS)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (source == null)
            {
                report.AppendLine($"프리팹을 찾을 수 없습니다: {path}");
                continue;
            }

            report.AppendLine($"== {System.IO.Path.GetFileNameWithoutExtension(path)}");

            foreach (float time in TIMES)
            {
                var instance = Object.Instantiate(source);
                instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Simulate(time, false, true, true);
                }

                float lowest = float.MaxValue;
                float highest = float.MinValue;
                string lowestName = "-";
                string highestName = "-";

                foreach (ParticleSystemRenderer renderer in
                    instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }

                    var ps = renderer.GetComponent<ParticleSystem>();

                    if (ps == null || ps.particleCount == 0)
                    {
                        continue;
                    }

                    Bounds bounds = renderer.bounds;

                    if (bounds.size.y < EMPTY_BOUNDS_EPSILON)
                    {
                        continue;
                    }

                    float bottom = bounds.min.y - STAGE_Y;
                    float top = bounds.max.y - STAGE_Y;

                    if (bottom < lowest)
                    {
                        lowest = bottom;
                        lowestName = renderer.gameObject.name;
                    }

                    if (top > highest)
                    {
                        highest = top;
                        highestName = renderer.gameObject.name;
                    }
                }

                report.Append($"  t={time:0.##}  밑동={lowest:0.###} ({lowestName})");
                report.AppendLine($"  꼭대기={highest:0.###} ({highestName})");

                Object.DestroyImmediate(instance);
            }
        }

        report.AppendLine();
        report.AppendLine("== 자식별 밑동 (t=0.2, 낮은 순 10개)");

        foreach (string path in IMPACT_PATHS)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (source == null)
            {
                continue;
            }

            var instance = Object.Instantiate(source);
            instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

            foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Simulate(0.2f, false, true, true);
            }

            report.AppendLine($"  -- {System.IO.Path.GetFileNameWithoutExtension(path)}");

            var lines = new System.Collections.Generic.List<(float Bottom, string Line)>();

            foreach (ParticleSystemRenderer renderer in
                instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var ps = renderer.GetComponent<ParticleSystem>();

                if (!renderer.enabled || ps == null || ps.particleCount == 0)
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;

                if (bounds.size.y < EMPTY_BOUNDS_EPSILON)
                {
                    continue;
                }

                float bottom = bounds.min.y - STAGE_Y;
                float top = bounds.max.y - STAGE_Y;

                lines.Add((bottom,
                    $"     {renderer.gameObject.name,-16} 밑동={bottom,7:0.###} 꼭대기={top,7:0.###} 개수={ps.particleCount}"));
            }

            lines.Sort((a, b) => a.Bottom.CompareTo(b.Bottom));

            for (int index = 0; index < lines.Count && index < 10; index++)
            {
                report.AppendLine(lines[index].Line);
            }

            Object.DestroyImmediate(instance);
        }

        return report.ToString();
    }
}
