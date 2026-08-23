using System.Text;
using UnityEditor;
using UnityEngine;

// 얼음 명중 이펙트의 바닥 데칼을 플레이에서 확인할 수 있게 프리팹에 직접 반영한다.
//
// HorizontalBillboard는 월드 XZ 평면에 고정돼 회전 0인 이 프로젝트의 카메라에서 옆날로 서서
// 아무것도 그리지 않는다. 렌더 모드를 Billboard로 바꾸고 세로만 눌러 바닥에 누운 타원으로 만든다.
//
// 이것은 검증용 손수정이다. BuildStatusVfx를 다시 돌리면 사라진다 -
// 확인이 끝나면 생성기 쪽에 정식으로 인코딩해야 한다.
public static class ApplyIceDecalBillboard
{
    private static readonly string[] TARGET_PATHS =
    {
        "Assets/Imported/Prefabs/Effects/Status/FX_Impact_IceCrack.prefab",
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab"
    };

    // 아이소메트릭 셀이 1 x 0.5(Grid.prefab m_CellSize)이므로 바닥에 누운 원은 화면에서 2:1 타원이다.
    private const float ISO_SQUASH = 0.5f;

    public static string Execute()
    {
        return Run(true);
    }

    public static string Revert()
    {
        return Run(false);
    }

    private static string Run(bool applies)
    {
        var report = new StringBuilder();

        foreach (string path in TARGET_PATHS)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents == null)
            {
                report.AppendLine($"프리팹을 찾을 수 없습니다: {path}");
                continue;
            }

            var lines = new StringBuilder();
            int changed = 0;

            foreach (ParticleSystem ps in contents.GetComponentsInChildren<ParticleSystem>(true))
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();

                if (renderer == null)
                {
                    continue;
                }

                ParticleSystemRenderMode from = applies
                    ? ParticleSystemRenderMode.HorizontalBillboard
                    : ParticleSystemRenderMode.Billboard;

                if (renderer.renderMode != from)
                {
                    continue;
                }

                // 되돌릴 때는 원래 HorizontalBillboard였던 것만 골라야 한다.
                // 처음부터 Billboard였던 자식은 startSize3D가 꺼져 있으므로 그것으로 구분한다.
                var main = ps.main;

                if (!applies && !main.startSize3D)
                {
                    continue;
                }

                if (applies)
                {
                    renderer.renderMode = ParticleSystemRenderMode.Billboard;
                    renderer.alignment = ParticleSystemRenderSpace.View;

                    ParticleSystem.MinMaxCurve width = main.startSizeX;
                    main.startSize3D = true;
                    main.startSizeY = Scaled(width, ISO_SQUASH);
                    main.startSizeZ = width;
                }
                else
                {
                    renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                    main.startSize3D = false;
                }

                changed++;
                lines.Append("  ").Append(ps.gameObject.name).AppendLine();
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            report.Append($"{(applies ? "적용" : "되돌림")} {changed}개 - {path}\n").Append(lines);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    private static ParticleSystem.MinMaxCurve Scaled(ParticleSystem.MinMaxCurve source, float multiplier)
    {
        switch (source.mode)
        {
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(
                    source.constantMin * multiplier, source.constantMax * multiplier);

            case ParticleSystemCurveMode.Curve:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curve);

            case ParticleSystemCurveMode.TwoCurves:
                return new ParticleSystem.MinMaxCurve(
                    source.curveMultiplier * multiplier, source.curveMin, source.curveMax);

            default:
                return new ParticleSystem.MinMaxCurve(source.constant * multiplier);
        }
    }
}
