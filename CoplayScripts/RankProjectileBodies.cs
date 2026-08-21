using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 발사체 원본 중 "날아가는 몸통이 실제로 보이는" 것만 고르기 위한 계측기.
// 몸통 = 렌더되고(mode != None), 트랜스폼을 따라다니는(Local) 시스템. 그 크기를 루트 스케일까지 곱해 잰다.
public static class RankProjectileBodies
{
    public static string Execute()
    {
        var rows = new List<(float body, float trail, string line)>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[]
                 {
                     "Assets/Imported/Prefabs/Projectile/AAA_Vol1",
                     "Assets/Imported/Prefabs/Projectile/AAA_Vol2"
                 }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

            if (!fileName.StartsWith("Projectile_", StringComparison.Ordinal))
            {
                continue;
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
            {
                continue;
            }

            float scale = root.transform.localScale.x;
            float body = 0f;
            float trail = 0f;
            int rendered = 0;
            var modes = new HashSet<string>();

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || renderer.renderMode == ParticleSystemRenderMode.None)
                {
                    continue;
                }

                // 늘어나는 빌보드는 입자 속도로 길이를 만든다. 우리 투사체는 트랜스폼만 움직이고
                // 입자 속도는 0이라 velocityScale이 0이면 화면에서 사실상 사라진다.
                bool degenerateStretch =
                    renderer.renderMode == ParticleSystemRenderMode.Stretch &&
                    Mathf.Approximately(renderer.velocityScale, 0f) &&
                    Mathf.Approximately(ps.main.startSpeed.constant, 0f);

                if (degenerateStretch)
                {
                    continue;
                }

                rendered += 1;
                modes.Add(renderer.renderMode.ToString().Substring(0, 2));

                float size = ps.main.startSize.constant * scale;

                if (ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                {
                    body = Mathf.Max(body, size);
                }
                else
                {
                    trail = Mathf.Max(trail, size);
                }
            }

            string line =
                $"{fileName,-38} body={body,-6:0.###} trail={trail,-6:0.###} scale={scale,-5:0.##} ps={rendered} {string.Join("/", modes)}";

            rows.Add((body, trail, line));
        }

        rows.Sort((a, b) => (b.body + b.trail).CompareTo(a.body + a.trail));

        var sb = new StringBuilder();
        foreach ((float _, float _, string line) in rows)
        {
            sb.AppendLine(line);
        }

        return sb.ToString();
    }
}
