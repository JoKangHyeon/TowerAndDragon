using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// 암석 명중 이펙트를 얼음과 같은 방식으로 손본다. 네 가지를 한 번에 한다.
//
// 1) Burst·AOE 제거
//    Burst : 바위가 단조로워서 얹었던 "탄이 쪼개지는 순간"(원본은 초록, 세트 색조 280으로
//            보라가 된 것). 바닥 데칼이 살아나면 필요 없다.
//    AOE   : FX_Crack_RockAOE를 통째로 얹은 광역 반경 표시. 자식 11개가 같이 사라진다.
//            이 팩의 AOE는 3D 지면을 내려다보는 카메라를 전제로 한 큰 바닥 판이라,
//            정사영 2D에 맞추려고 빌보드로 세우면 솟아오른 바위를 정면에서 덮어 버린다.
//            대신 들어간 것은 없다 - 광역 반경 표시는 지금 없는 상태다.
// 2) 색조를 원본으로 : 보라를 쓰지 않기로 했다. 데칼만 원본색으로 두면 바위·먼지만 보라로 남는다.
// 3) HorizontalBillboard -> Billboard + 2:1 눌림 : 월드 XZ 평면에 고정된 렌더 모드라 회전 0인
//    이 프로젝트의 카메라에서는 옆날로 서서 아무것도 그리지 않는다.
// 4) 가산합성 레이어의 Start Color를 HDR(1 초과)로 : 블룸을 쓸 수 없어서 대신 쓴다.
//
// rock·Mountain(솟는 바위)은 건드리지 않는다.
//
// 모든 값을 원본 팩 프리팹에서 다시 계산한다. 현재 값에 곱하면 두 번 돌릴 때마다 커진다.
// AOE를 먼저 지우므로 아래 순회는 FX_Crack_Rock 자식만 본다 - 이름이 어느 원본 것인지
// 갈라 볼 필요가 없다. 이 순서를 뒤집으면 ground가 두 원본에 겹쳐 엉뚱한 값을 읽는다.
//
// 이것은 검증용 손수정이다. BuildStatusVfx를 다시 돌리면 사라진다.
// (단 AOE는 생성기에서도 뺐으므로 다시 돌려도 살아나지 않는다.)
public static class ApplyStoneDecalTuning
{
    private const string BASELINE_PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Crack_Rock.prefab";

    private static readonly string[] TARGET_PATHS =
    {
        "Assets/Imported/Prefabs/Effects/Status/FX_Impact_StoneCrack.prefab",
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab"
    };

    private static readonly string[] REMOVED_CHILDREN = { "Burst", "AOE" };

    private const float ISO_SQUASH = 0.5f;

    private struct DecalTuning
    {
        public float Size;
        public float Color;

        public DecalTuning(float size, float color)
        {
            Size = size;
            Color = color;
        }
    }

    // 자식마다 크기·색 배수를 따로 준다. 하나의 배수로는 맞출 수 없다 -
    // ground는 회색 먼지라 색을 올리면 흰 덩어리가 되어 바위를 덮고,
    // ring은 3배라야 바닥 링으로 읽힌다.
    //
    // AOE 쪽 자식(floor_glowC·shangmianglow·floor_glow2·decal)에도 배수를 주고 있었지만
    // AOE를 통째로 뺐으므로 같이 지웠다.
    //
    // 색 1은 원본 그대로라는 뜻이다(세트 색조 보라를 푸는 역할은 그대로 한다).
    // ground·ground (1)은 URP_AlphaBlendFlow라 색을 올려도 소용이 없어 1로 둔다.
    private static readonly Dictionary<string, DecalTuning> DECAL_TUNINGS =
        new Dictionary<string, DecalTuning>
    {
        // 회색 먼지. 키우면 흰 덩어리가 되어 바위를 덮는다.
        { "ground", new DecalTuning(0.5f, 1f) },
        { "ground (1)", new DecalTuning(0.5f, 1f) },

        // 바닥 균열·링. 이 둘이 "지면에 꽂혔다"를 만든다.
        // 반경 표시는 아니다 - ring은 18x2에 루트 0.08이 곱해져 2.88칸이라 반경 2(지름 4칸)보다 작다.
        { "glow", new DecalTuning(2f, 3f) },
        { "ring", new DecalTuning(2f, 3f) }
    };

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
        var baseline = AssetDatabase.LoadAssetAtPath<GameObject>(BASELINE_PATH);

        if (baseline == null)
        {
            return "원본 프리팹을 찾을 수 없습니다";
        }

        var report = new StringBuilder();
        report.AppendLine(applies ? "적용 - 자식별 크기·색 배수" : "되돌림 - 원본 팩 값으로");

        foreach (string path in TARGET_PATHS)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            if (contents == null)
            {
                report.AppendLine($"프리팹을 찾을 수 없습니다: {path}");
                continue;
            }

            // 되돌려도 살아나지 않는다 - Burst는 생성기가 다시 얹어야 하고,
            // AOE는 생성기에서도 뺐으므로 아예 돌아오지 않는다.
            if (applies)
            {
                foreach (string name in REMOVED_CHILDREN)
                {
                    Transform doomed = FindChild(contents.transform, name);

                    if (doomed != null)
                    {
                        UnityEngine.Object.DestroyImmediate(doomed.gameObject);
                        report.AppendLine($"  제거: {name}");
                    }
                }
            }

            foreach (ParticleSystem ps in contents.GetComponentsInChildren<ParticleSystem>(true))
            {
                string name = ps.gameObject.name;

                // AOE를 위에서 지웠으므로 여기 남은 것은 전부 FX_Crack_Rock의 자식이다.
                ParticleSystem original = Find(baseline, name);

                if (original == null)
                {
                    continue;
                }

                var originalMain = original.main;
                var main = ps.main;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();

                DecalTuning tuning;
                bool isDecal = DECAL_TUNINGS.TryGetValue(name, out tuning);

                if (isDecal)
                {
                    ParticleSystem.MinMaxCurve width = ScaledCurve(
                        originalMain.startSizeX, applies ? tuning.Size : 1f);

                    if (applies)
                    {
                        renderer.renderMode = ParticleSystemRenderMode.Billboard;
                        renderer.alignment = ParticleSystemRenderSpace.View;

                        main.startSize3D = true;
                        main.startSizeX = width;
                        main.startSizeY = ScaledCurve(width, ISO_SQUASH);
                        main.startSizeZ = width;
                    }
                    else
                    {
                        var originalRenderer = original.GetComponent<ParticleSystemRenderer>();
                        renderer.renderMode = originalRenderer.renderMode;
                        renderer.alignment = originalRenderer.alignment;

                        main.startSize3D = false;
                        main.startSize = width;
                    }

                    report.Append("  ").Append(name.PadRight(16));
                    report.Append("크기 ").Append(originalMain.startSizeX.constant.ToString("0.##"));
                    report.Append(" -> ").Append(width.constant.ToString("0.##").PadRight(8));
                    report.Append("색 x").AppendLine(applies ? tuning.Color.ToString("0.##") : "1");
                }

                float colorMultiplier = applies && isDecal ? tuning.Color : 1f;

                // 알파는 그대로 둔다. 알파를 건드리면 페이드 아웃 타이밍이 같이 바뀐다.
                // 이 대입이 세트 색조(보라)를 원본색으로 되돌리는 역할도 한다.
                Color source = originalMain.startColor.color;
                main.startColor = new Color(
                    source.r * colorMultiplier,
                    source.g * colorMultiplier,
                    source.b * colorMultiplier,
                    source.a);
            }

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            report.AppendLine($"  저장: {path}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != root && child.name == name)
            {
                return child;
            }
        }

        return null;
    }

    private static ParticleSystem Find(GameObject root, string name)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.gameObject.name == name)
            {
                return ps;
            }
        }

        return null;
    }

    private static ParticleSystem.MinMaxCurve ScaledCurve(ParticleSystem.MinMaxCurve source, float multiplier)
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
