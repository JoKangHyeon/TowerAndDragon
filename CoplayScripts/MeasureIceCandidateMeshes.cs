using System.Text;
using UnityEditor;
using UnityEngine;

// 병합 배수를 정하기 전에 메시 바운즈와 부모 체인 스케일을 계측한다.
// MergeChildren은 자식을 루트에 바로 붙이므로 부모 컨테이너의 스케일이 사라진다.
// Mesh 모드는 startSize에 메시 바운즈까지 곱해지므로 그것도 같이 본다.
public static class MeasureIceCandidateMeshes
{
    private const string SOURCE_FOLDER =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    // 원본 이름 : 자식 이름
    private static readonly string[][] TARGETS =
    {
        new[] { "FX_Crack_BlueShock", "shock_up" },
        new[] { "FX_Crack_BlueShock", "shock_up (1)" },
        new[] { "FX_Crack_BlueShock", "shock_up (2)" },
        new[] { "FX_Crack_BlueShock", "ring02" },
        new[] { "FX_Crack_BlueShock", "wave01" },
        new[] { "FX_Crack_Blue02", "lms" },
        new[] { "FX_Crack_Blue02", "Particle_up" },
        new[] { "FX_Crack_Blue02", "Flare" },
        new[] { "FX_Crack_Bluerock", "rock1" },
        new[] { "FX_GroundCrack_Blue", "ground" }
    };

    public static string Execute()
    {
        var sb = new StringBuilder();
        sb.AppendLine("자식            startSize localScale 부모체인 메시장축 유효크기(x0.05)");

        foreach (string[] target in TARGETS)
        {
            GameObject donor = PrefabUtility.LoadPrefabContents($"{SOURCE_FOLDER}/{target[0]}.prefab");

            try
            {
                Transform child = FindChild(donor, target[1]);

                if (child == null)
                {
                    sb.AppendLine($"{target[1]} MISSING in {target[0]}");
                    continue;
                }

                var ps = child.GetComponent<ParticleSystem>();
                var renderer = child.GetComponent<ParticleSystemRenderer>();

                float startSize = ps.main.startSize.constant;
                float chain = ParentChainScale(child, donor.transform);
                bool usesMesh = renderer != null &&
                                renderer.renderMode == ParticleSystemRenderMode.Mesh &&
                                renderer.mesh != null;
                float meshAxis = usesMesh ? LongestAxis(renderer.mesh.bounds) : 1f;
                float effective = startSize * child.localScale.x * meshAxis * ROOT_MULTIPLIER;

                sb.Append(target[1].PadRight(15));
                sb.Append(startSize.ToString("0.##").PadRight(10));
                sb.Append(child.localScale.x.ToString("0.##").PadRight(11));
                sb.Append(chain.ToString("0.##").PadRight(9));
                sb.Append((usesMesh ? meshAxis.ToString("0.##") : "-(빌보드)").PadRight(9));
                sb.Append(effective.ToString("0.###"));
                sb.AppendLine($"   부모체인 복원시 {effective * chain:0.###}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(donor);
            }
        }

        return sb.ToString();
    }

    private const float ROOT_MULTIPLIER = 0.05f;

    private static float ParentChainScale(Transform child, Transform root)
    {
        float scale = 1f;
        Transform cursor = child.parent;

        while (cursor != null && cursor != root.parent)
        {
            scale *= cursor.localScale.x;
            cursor = cursor.parent;
        }

        return scale;
    }

    private static float LongestAxis(Bounds bounds)
    {
        return Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
    }

    private static Transform FindChild(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
            {
                return t;
            }
        }

        return null;
    }
}
