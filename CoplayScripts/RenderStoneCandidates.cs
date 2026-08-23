using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 암석 명중 이펙트 후보를 굽는다. 얼음과 같은 처리를 하되 세 가지가 다르다.
//
// 1) 얹어 둔 AOE·Burst를 뺀다. 바위가 단조로워서 붙였던 것인데, 바닥 데칼이 살아나면 필요 없다.
// 2) 색조를 원본으로 되돌린다. 지금 클론은 HueDegrees 280으로 보라인데, 데칼만 원본색으로 두면
//    바위·먼지만 보라로 남아 따로 논다.
// 3) 얼음의 blue_crack에 해당하는 가산합성 균열이 없다. 이 팩의 암석 균열(ground)은
//    URP_AlphaBlendFlow라 색을 올릴 수 없고 크기로만 조절한다.
//
// rock·Mountain(솟는 바위)은 어느 후보에서도 건드리지 않는다.
// 프리팹 에셋도 건드리지 않는다 - 씬에 만든 인스턴스에서만 바꿔 찍는다.
public static class RenderStoneCandidates
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_StoneMeteor.prefab";

    private const string BASELINE_PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Crack_Rock.prefab";

    private const string AOE_BASELINE_PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Crack_RockAOE.prefab";

    // 탄이 쪼개지는 순간(원본은 초록, 세트 색조 280으로 보라가 된 것). 이것만 뺀다.
    private static readonly string[] REMOVED_CHILDREN = { "Burst" };

    // 광역 표시의 뿌리. 원본에서는 이름이 "Particle System"이라 기준값을 찾을 때 바꿔 줘야 한다.
    private const string AOE_ROOT = "AOE";
    private const string AOE_ROOT_IN_BASELINE = "Particle System";

    // 되살릴 바닥 데칼 전부. 넷은 FX_Crack_Rock, 여섯은 AOE 아래(FX_Crack_RockAOE)에서 왔다.
    // AOE 쪽은 여섯 개가 전부 HorizontalBillboard라 광역 표시가 통째로 안 그려지고 있었다.
    private static readonly string[] DECAL_NAMES =
    {
        "glow", "ring", "ground", "ground (1)",
        "AOE", "shangmianglow", "floor_glow2", "floor_glowC", "decal"
    };

    // 그중 가산합성이라 색을 올릴 수 있는 것들.
    // ground·ground (1)·floor_glowC는 URP_AlphaBlendFlow라 제외한다.
    private static readonly string[] BOOSTED_NAMES =
    {
        "glow", "ring", "shangmianglow", "AOE", "floor_glow2", "decal"
    };

    private const float ISO_SQUASH = 0.5f;

    private sealed class Candidate
    {
        public string Label;
        public bool RemovesExtras;
        public bool RestoresOriginalHue;
        public bool ConvertsDecals;
        public float ColorMultiplier = 1f;
        public Dictionary<string, float> SizeMultipliers = new Dictionary<string, float>();
    }

    private static readonly Candidate[] CANDIDATES =
    {
        new Candidate { Label = "A 지금 그대로(Burst 있음, 보라)" },

        new Candidate
        {
            Label = "B Burst 제거 + 원본색 + 변환만",
            RemovesExtras = true,
            RestoresOriginalHue = true,
            ConvertsDecals = true
        },

        new Candidate
        {
            Label = "C 색3 + 먼지 절반",
            RemovesExtras = true,
            RestoresOriginalHue = true,
            ConvertsDecals = true,
            ColorMultiplier = 3f,
            SizeMultipliers = new Dictionary<string, float>
            {
                { "ground", 0.5f }, { "ground (1)", 0.5f }
            }
        },

        new Candidate
        {
            Label = "D C + 글로우·링 2배 + 광역 표시 1.5배",
            RemovesExtras = true,
            RestoresOriginalHue = true,
            ConvertsDecals = true,
            ColorMultiplier = 3f,
            SizeMultipliers = new Dictionary<string, float>
            {
                { "ground", 0.5f }, { "ground (1)", 0.5f },
                { "glow", 2f }, { "ring", 2f },
                { "shangmianglow", 1.5f }, { "floor_glow2", 1.5f }, { "decal", 1.5f }
            }
        }
    };

    private static readonly float[] TIMES = { 0.04f, 0.1f, 0.2f, 0.35f, 0.6f };

    private const int PANEL = 600;

    // 플레이어가 실제로 갈 수 있는 최대 확대(CameraController 최소 줌 3). 게임 기본은 5다.
    private const float ORTHO_SIZE = 3f;

    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);
        var baseline = AssetDatabase.LoadAssetAtPath<GameObject>(BASELINE_PATH);
        var aoeBaseline = AssetDatabase.LoadAssetAtPath<GameObject>(AOE_BASELINE_PATH);

        if (source == null || baseline == null || aoeBaseline == null)
        {
            return "프리팹을 찾을 수 없습니다";
        }

        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * CANDIDATES.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("StoneCandidateCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        camera.allowHDR = true;

        var target = new RenderTexture(PANEL, PANEL, 24, RenderTextureFormat.DefaultHDR);

        try
        {
            for (int row = 0; row < CANDIDATES.Length; row++)
            {
                // 텍스처는 y가 위로 자라므로 첫 후보가 맨 윗줄에 오도록 뒤집는다.
                int rowY = (CANDIDATES.Length - 1 - row) * PANEL;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    var instance = UnityEngine.Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                    Tune(instance, baseline, aoeBaseline, CANDIDATES[row]);

                    foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ps.Simulate(TIMES[index], false, true, true);
                    }

                    camera.targetTexture = target;
                    camera.Render();

                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;

                    var panel = new Texture2D(PANEL, PANEL, TextureFormat.RGBA32, false);
                    panel.ReadPixels(new Rect(0f, 0f, PANEL, PANEL), 0, 0);
                    panel.Apply();

                    RenderTexture.active = previous;

                    sheet.SetPixels(index * PANEL, rowY, PANEL, PANEL, panel.GetPixels());

                    UnityEngine.Object.DestroyImmediate(panel);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "stone_candidates.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            var labels = new List<string>();

            foreach (Candidate candidate in CANDIDATES)
            {
                labels.Add(candidate.Label);
            }

            return $"{path}\n줄(위→아래): {string.Join(" / ", labels)}\n" +
                   $"칸(왼→오른) 시각(초): {string.Join(", ", TIMES)}\n" +
                   $"프레이밍: 직교 크기 {ORTHO_SIZE} (최대 확대)";
        }
        finally
        {
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(sheet);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Tune(
        GameObject root, GameObject baseline, GameObject aoeBaseline, Candidate candidate)
    {
        if (candidate.RemovesExtras)
        {
            foreach (string name in REMOVED_CHILDREN)
            {
                Transform doomed = FindChild(root.transform, name);

                if (doomed != null)
                {
                    UnityEngine.Object.DestroyImmediate(doomed.gameObject);
                }
            }
        }

        Transform aoeRoot = FindChild(root.transform, AOE_ROOT);

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            string name = ps.gameObject.name;

            // ground는 두 원본에 같은 이름으로 있다. AOE 아래인지로 갈라야 엉뚱한 값을 읽지 않는다.
            bool underAoe = aoeRoot != null && ps.transform.IsChildOf(aoeRoot);
            GameObject lookupRoot = underAoe ? aoeBaseline : baseline;
            string lookupName = name == AOE_ROOT ? AOE_ROOT_IN_BASELINE : name;

            ParticleSystem original = Find(lookupRoot, lookupName);

            if (original == null)
            {
                continue;
            }

            var originalMain = original.main;
            var main = ps.main;
            bool isDecal = Array.IndexOf(DECAL_NAMES, name) >= 0;

            float sizeMultiplier;

            if (!candidate.SizeMultipliers.TryGetValue(name, out sizeMultiplier))
            {
                sizeMultiplier = 1f;
            }

            if (isDecal && candidate.ConvertsDecals)
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;

                ParticleSystem.MinMaxCurve width = ScaledCurve(originalMain.startSizeX, sizeMultiplier);

                main.startSize3D = true;
                main.startSizeX = width;
                main.startSizeY = ScaledCurve(width, ISO_SQUASH);
                main.startSizeZ = width;
            }

            if (!candidate.RestoresOriginalHue)
            {
                continue;
            }

            float colorMultiplier =
                Array.IndexOf(BOOSTED_NAMES, name) >= 0 ? candidate.ColorMultiplier : 1f;

            // 알파는 그대로 둔다. 알파를 건드리면 페이드 아웃 타이밍이 같이 바뀐다.
            Color source = originalMain.startColor.color;
            main.startColor = new Color(
                source.r * colorMultiplier,
                source.g * colorMultiplier,
                source.b * colorMultiplier,
                source.a);
        }
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

    private static readonly string[] ISOLATED_NAMES =
    {
        "AOE", "shangmianglow", "floor_glow2", "floor_glowC", "decal", "ground"
    };

    // AOE 자식을 하나씩만 남기고 변환해 굽는다. 합쳐 놓으면 어느 것이 흰 판을 만드는지 알 수 없다.
    public static string ExecuteIsolated()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);
        var baseline = AssetDatabase.LoadAssetAtPath<GameObject>(BASELINE_PATH);
        var aoeBaseline = AssetDatabase.LoadAssetAtPath<GameObject>(AOE_BASELINE_PATH);

        if (source == null || baseline == null || aoeBaseline == null)
        {
            return "프리팹을 찾을 수 없습니다";
        }

        const int isolatedPanel = 400;

        var candidate = new Candidate
        {
            Label = "변환만",
            RemovesExtras = true,
            RestoresOriginalHue = true,
            ConvertsDecals = true
        };

        var sheet = new Texture2D(
            isolatedPanel * TIMES.Length, isolatedPanel * ISOLATED_NAMES.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("StoneIsolateCamera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = ORTHO_SIZE;
        camera.transform.position = new Vector3(0f, STAGE_Y, -CAMERA_DISTANCE);
        camera.transform.rotation = Quaternion.identity;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        camera.allowHDR = true;

        var target = new RenderTexture(isolatedPanel, isolatedPanel, 24, RenderTextureFormat.DefaultHDR);

        try
        {
            for (int row = 0; row < ISOLATED_NAMES.Length; row++)
            {
                int rowY = (ISOLATED_NAMES.Length - 1 - row) * isolatedPanel;

                for (int index = 0; index < TIMES.Length; index++)
                {
                    var instance = UnityEngine.Object.Instantiate(source);
                    instance.transform.position = new Vector3(0f, STAGE_Y, 0f);

                    Tune(instance, baseline, aoeBaseline, candidate);
                    Isolate(instance, ISOLATED_NAMES[row]);

                    foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ps.Simulate(TIMES[index], false, true, true);
                    }

                    camera.targetTexture = target;
                    camera.Render();

                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;

                    var panel = new Texture2D(isolatedPanel, isolatedPanel, TextureFormat.RGBA32, false);
                    panel.ReadPixels(new Rect(0f, 0f, isolatedPanel, isolatedPanel), 0, 0);
                    panel.Apply();

                    RenderTexture.active = previous;

                    sheet.SetPixels(index * isolatedPanel, rowY, isolatedPanel, isolatedPanel, panel.GetPixels());

                    UnityEngine.Object.DestroyImmediate(panel);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            sheet.Apply();

            string path = Path.Combine(Path.GetTempPath(), "stone_isolated.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            return $"{path}\n줄(위→아래): {string.Join(", ", ISOLATED_NAMES)}\n" +
                   $"칸(왼→오른) 시각(초): {string.Join(", ", TIMES)}";
        }
        finally
        {
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            UnityEngine.Object.DestroyImmediate(sheet);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void Isolate(GameObject root, string keptName)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer != null && ps.gameObject.name != keptName)
            {
                renderer.enabled = false;
            }
        }
    }
}
