using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태 연출 세 종을 실제 몬스터 스프라이트 위에 얹어 굽는다.
///
/// 확인하려는 것은 프리팹 단독 크기가 아니라 <b>몬스터 대비 크기</b>다.
/// 빙결이 루트 스케일 0.05로 다른 둘(0.18)보다 훨씬 작은데, 원본 authoring 기준이 달라
/// 숫자만으로는 실제로 작은지 알 수 없다(작업노트 3-7).
///
/// MonsterStatusVfx와 같은 방식으로 위치를 잡는다 - SpriteRenderer 경계의 중심.
/// 몬스터는 크기 사다리의 양 끝과 가운데를 쓴다.
/// </summary>
public static class RenderStatusVfxOnMonster
{
    private const string VFX_FOLDER = "Assets/Imported/Prefabs/Effects/Status";
    private const string MONSTER_FOLDER = "Assets/Data/MonsterData/MonsterPrefab";
    private const string SHEET_PATH = "output/vfx_check/status_vfx_on_monster.png";
    private const string OUTPUT_PATH = "output/vfx_check/status_vfx_on_monster.txt";

    private const int FRAME_SIZE = 320;
    private const float CAMERA_DISTANCE = 10f;
    private const float ALPHA_THRESHOLD = 0.08f;
    private const int MEASURE_LAYER = 31;

    // 연출이 완성 형태에 도달한 시점. 세 종 다 1.5초면 안정됐다.
    private const float SIMULATE_TIME = 1.5f;

    private static readonly string[] VFX_NAMES =
    {
        "FX_Status_FireBurn",
        "FX_Status_IceSlow",
        "FX_Status_IceFreeze",
    };

    private static readonly string[] MONSTER_NAMES =
    {
        "MP_Ground_ElementImmune_Stone", // 루트 0.1 - 가장 작다
        "MP_Ground_Tank",                // 1.0
        "MP_Boss_2",                     // 1.5 - 가장 크다
    };

    public static void Execute()
    {
        var report = new StringBuilder();
        report.AppendLine("[RenderStatusVfxOnMonster]");
        report.AppendLine($"  t={SIMULATE_TIME}초 · 위치는 스프라이트 경계 중심(MonsterStatusVfx와 동일)");
        report.AppendLine();

        var sheet = new Texture2D(
            FRAME_SIZE * MONSTER_NAMES.Length,
            FRAME_SIZE * VFX_NAMES.Length,
            TextureFormat.RGBA32,
            false);

        var target = new RenderTexture(FRAME_SIZE, FRAME_SIZE, 24, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(FRAME_SIZE, FRAME_SIZE, TextureFormat.RGBA32, false);

        report.AppendLine("연출 폭 / 몬스터 폭 (1에 가까우면 몬스터를 덮는다):");
        report.Append("  연출 ");

        foreach (string monsterName in MONSTER_NAMES)
        {
            report.Append($"| {monsterName} ");
        }

        report.AppendLine();

        for (int v = 0; v < VFX_NAMES.Length; v++)
        {
            var vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{VFX_FOLDER}/{VFX_NAMES[v]}.prefab");

            if (vfxPrefab == null)
            {
                report.AppendLine($"  {VFX_NAMES[v]}: 프리팹 없음");
                continue;
            }

            report.Append($"  {VFX_NAMES[v]} ");

            for (int m = 0; m < MONSTER_NAMES.Length; m++)
            {
                var monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{MONSTER_FOLDER}/{MONSTER_NAMES[m]}.prefab");

                if (monsterPrefab == null)
                {
                    report.Append("| 없음 ");
                    continue;
                }

                GameObject monster = Object.Instantiate(monsterPrefab);
                monster.transform.position = Vector3.zero;
                SetLayerRecursive(monster, MEASURE_LAYER);
                StripBehaviours(monster);

                SpriteRenderer body = ResolveBody(monster.transform);
                Bounds bounds = body != null
                    ? body.bounds
                    : new Bounds(Vector3.zero, Vector3.one);

                GameObject vfx = Object.Instantiate(vfxPrefab);
                SetLayerRecursive(vfx, MEASURE_LAYER);
                vfx.transform.position = new Vector3(bounds.center.x, bounds.center.y, 0f);

                var systems = vfx.GetComponentsInChildren<ParticleSystem>(true);
                ParticleSystem vfxRoot = vfx.GetComponent<ParticleSystem>();

                foreach (ParticleSystem particles in systems)
                {
                    particles.Clear(true);
                }

                if (vfxRoot != null)
                {
                    vfxRoot.Simulate(SIMULATE_TIME, true, true);
                }
                else
                {
                    foreach (ParticleSystem particles in systems)
                    {
                        particles.Simulate(SIMULATE_TIME, true, true);
                    }
                }

                // 몬스터가 프레임에 꽉 차게 배율을 맞춘다 - 그래야 "몬스터 대비"가 눈에 보인다.
                float ortho = Mathf.Max(bounds.size.x, bounds.size.y) * 1.1f;
                GameObject cameraHost = CreateCamera(out Camera camera, bounds.center, ortho);
                camera.targetTexture = target;
                camera.Render();

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, FRAME_SIZE, FRAME_SIZE), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;

                sheet.SetPixels(
                    FRAME_SIZE * m,
                    FRAME_SIZE * (VFX_NAMES.Length - 1 - v),
                    FRAME_SIZE,
                    FRAME_SIZE,
                    readback.GetPixels());

                // 연출만 따로 재서 폭 비율을 얻는다.
                monster.SetActive(false);
                camera.Render();

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, FRAME_SIZE, FRAME_SIZE), 0, 0);
                readback.Apply();
                RenderTexture.active = previous;

                float vfxWidth = MeasureWidthRatio(readback.GetPixels());

                report.Append($"| {vfxWidth:0.00} ");

                camera.targetTexture = null;
                Object.DestroyImmediate(cameraHost);
                Object.DestroyImmediate(vfx);
                Object.DestroyImmediate(monster);
            }

            report.AppendLine();
        }

        sheet.Apply();
        System.IO.File.WriteAllBytes(SHEET_PATH, sheet.EncodeToPNG());

        RenderTexture.active = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(readback);
        Object.DestroyImmediate(sheet);

        report.AppendLine();
        report.AppendLine($"시트: {SHEET_PATH}");
        report.AppendLine($"  가로: {string.Join(" / ", MONSTER_NAMES)}");
        report.AppendLine($"  세로(위→아래): {string.Join(" / ", VFX_NAMES)}");

        System.IO.File.WriteAllText(OUTPUT_PATH, report.ToString());
        Debug.Log(report.ToString());
    }

    // 프레임 폭 대비 연출이 차지하는 가로 비율. 프레임이 몬스터 폭에 맞춰져 있으므로
    // 이 값이 곧 "몬스터 대비 연출 폭"이다.
    private static float MeasureWidthRatio(Color[] pixels)
    {
        int minX = int.MaxValue;
        int maxX = int.MinValue;

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a <= ALPHA_THRESHOLD)
            {
                continue;
            }

            int x = i % FRAME_SIZE;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
        }

        if (minX > maxX)
        {
            return 0f;
        }

        return (maxX - minX + 1) / (float)FRAME_SIZE;
    }

    private static SpriteRenderer ResolveBody(Transform owner)
    {
        SpriteRenderer body = owner.GetComponent<SpriteRenderer>();

        return body != null ? body : owner.GetComponentInChildren<SpriteRenderer>(true);
    }

    // 스포너 없이 Instantiate하면 Awake/Start가 널 참조로 예외를 던진다 - 그림만 필요하다.
    private static void StripBehaviours(GameObject root)
    {
        var behaviours = new List<MonoBehaviour>(root.GetComponentsInChildren<MonoBehaviour>(true));

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                Object.DestroyImmediate(behaviour);
            }
        }
    }

    private static void SetLayerRecursive(GameObject root, int layer)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = layer;
        }
    }

    private static GameObject CreateCamera(out Camera camera, Vector3 center, float orthoSize)
    {
        var host = new GameObject("StatusVfxOnMonsterCamera (temp)");
        camera = host.AddComponent<Camera>();

        camera.orthographic = true;
        camera.orthographicSize = orthoSize;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.transform.position = new Vector3(center.x, center.y, -CAMERA_DISTANCE);
        camera.cullingMask = 1 << MEASURE_LAYER;

        return host;
    }
}
