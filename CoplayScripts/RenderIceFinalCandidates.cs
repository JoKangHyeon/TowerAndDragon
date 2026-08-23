using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// 데칼 튜닝 후보를 실제 게임 카메라와 같은 프레이밍(직교 크기 5)으로 굽는다.
//
// 값은 매번 원본 팩 프리팹에서 다시 계산한다. 현재 프리팹 값에 곱하면 이미 반영해 둔 튜닝 위에
// 또 곱해져서 A줄(지금)이 A가 아니게 된다.
//
// rock(솟는 얼음 창)은 어느 후보에서도 건드리지 않는다.
// 프리팹 에셋도 건드리지 않는다 - 씬에 만든 인스턴스에서만 바꿔 찍는다.
public static class RenderIceFinalCandidates
{
    private const string IMPACT_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Ice.prefab";

    private const string BASELINE_PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_GroundCrack_Blue.prefab";

    // 가산합성이라 색을 올릴 수 있는 것들. ground는 URP_AlphaBlendFlow라 제외한다.
    private static readonly string[] BOOSTED_NAMES = { "glow", "nova", "blue_crack" };

    // 손댈 바닥 데칼 전부. 이 넷 말고는 어느 후보에서도 건드리지 않는다.
    private static readonly string[] DECAL_NAMES = { "glow", "nova", "blue_crack", "ground" };

    private const float ISO_SQUASH = 0.5f;

    private sealed class Candidate
    {
        public string Label;

        // 끄면 원본 그대로(HorizontalBillboard) 둔다 - 화면에 아무것도 안 그려지는 상태의 재현.
        public bool ConvertsDecals = true;

        public float ColorMultiplier = 1f;
        public Dictionary<string, float> SizeMultipliers = new Dictionary<string, float>();
    }

    private static readonly Candidate[] CANDIDATES =
    {
        new Candidate { Label = "A 손대기 전", ConvertsDecals = false },

        new Candidate
        {
            Label = "B 변환만 (색1 크기1)",
            SizeMultipliers = new Dictionary<string, float>
            {
                { "glow", 1f }, { "nova", 1f }, { "blue_crack", 1f }, { "ground", 1f }
            }
        },

        new Candidate
        {
            Label = "C 색3 / 글로우·균열 2배 / 먼지 그대로",
            ColorMultiplier = 3f,
            SizeMultipliers = new Dictionary<string, float>
            {
                { "glow", 2f }, { "nova", 2f }, { "blue_crack", 2f }, { "ground", 1f }
            }
        },

        new Candidate
        {
            Label = "D 색3 / 균열 3배 / 글로우 2배 / 먼지 절반",
            ColorMultiplier = 3f,
            SizeMultipliers = new Dictionary<string, float>
            {
                { "glow", 2f }, { "nova", 2f }, { "blue_crack", 3f }, { "ground", 0.5f }
            }
        }
    };

    private static readonly float[] TIMES = { 0.04f, 0.08f, 0.14f, 0.22f, 0.4f };

    private const int PANEL = 600;

    // 실제 게임 카메라와 같은 값(SampleScene Main Camera: orthographic size 5, m_HDR 1).
    private const float ORTHO_SIZE = 5f;

    private const float CAMERA_DISTANCE = 10f;
    private const float STAGE_Y = -200f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(IMPACT_PATH);
        var baseline = AssetDatabase.LoadAssetAtPath<GameObject>(BASELINE_PATH);

        if (source == null || baseline == null)
        {
            return "프리팹을 찾을 수 없습니다";
        }

        var sheet = new Texture2D(PANEL * TIMES.Length, PANEL * CANDIDATES.Length, TextureFormat.RGBA32, false);

        var cameraObject = new GameObject("IceCandidateCamera");
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

                    Tune(instance, baseline, CANDIDATES[row]);

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

            string path = Path.Combine(Path.GetTempPath(), "ice_candidates_gameframe.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());

            var labels = new List<string>();

            foreach (Candidate candidate in CANDIDATES)
            {
                labels.Add(candidate.Label);
            }

            return $"{path}\n줄(위→아래): {string.Join(" / ", labels)}\n" +
                   $"칸(왼→오른) 시각(초): {string.Join(", ", TIMES)}\n" +
                   $"프레이밍: 직교 크기 {ORTHO_SIZE} (게임 카메라와 동일)";
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

    private static void Tune(GameObject root, GameObject baseline, Candidate candidate)
    {
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            string name = ps.gameObject.name;

            if (Array.IndexOf(DECAL_NAMES, name) < 0)
            {
                continue;
            }

            float sizeMultiplier;

            if (!candidate.SizeMultipliers.TryGetValue(name, out sizeMultiplier))
            {
                sizeMultiplier = 1f;
            }

            ParticleSystem original = Find(baseline, name);

            if (original == null)
            {
                continue;
            }

            var originalMain = original.main;
            var main = ps.main;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            ParticleSystem.MinMaxCurve width = ScaledCurve(
                originalMain.startSizeX, candidate.ConvertsDecals ? sizeMultiplier : 1f);

            if (candidate.ConvertsDecals)
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
                renderer.renderMode = original.GetComponent<ParticleSystemRenderer>().renderMode;
                renderer.alignment = original.GetComponent<ParticleSystemRenderer>().alignment;

                main.startSize3D = false;
                main.startSize = width;
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
