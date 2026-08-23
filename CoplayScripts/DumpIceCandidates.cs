using System.Text;
using UnityEditor;
using UnityEngine;

// 얼음 임팩트를 화려하게 만들 후보 원본들의 자식 구성을 계측한다.
// 병합 정의를 쓰기 전에 크기 자릿수·수명·루프·렌더모드를 먼저 봐야 한다.
public static class DumpIceCandidates
{
    private const string SOURCE_FOLDER =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP";

    private static readonly string[] SOURCES =
    {
        "FX_Crack_BlueShock", "FX_Crack_Blue02", "FX_GroundCrack_Blue", "FX_Crack_Bluerock"
    };

    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string name in SOURCES)
        {
            string path = $"{SOURCE_FOLDER}/{name}.prefab";
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (root == null)
            {
                sb.AppendLine($"== {name}  MISSING");
                continue;
            }

            sb.AppendLine($"== {name}  rootScale={root.transform.localScale.x:0.###}");

            foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var emission = ps.emission;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                Transform t = ps.transform;

                sb.Append("   ").Append(ps.gameObject.name.PadRight(14));
                sb.Append(" parent=").Append((t.parent == null ? "-" : t.parent.name).PadRight(20));
                sb.Append(" mode=").Append((renderer == null ? "-" : renderer.renderMode.ToString()).PadRight(20));
                sb.Append(" mesh=").Append((renderer == null || renderer.mesh == null ? "-" : renderer.mesh.name).PadRight(10));
                sb.Append(" size=").Append(main.startSize.constant.ToString("0.##").PadRight(7));
                sb.Append(" life=").Append(main.startLifetime.constant.ToString("0.##").PadRight(6));
                sb.Append(" loop=").Append(main.loop ? "1" : "0");
                sb.Append(" burst=").Append(emission.burstCount.ToString().PadRight(3));
                sb.Append(" space=").Append(main.simulationSpace == ParticleSystemSimulationSpace.World ? "W" : "L");
                sb.Append(" color=#").Append(Hex(main.startColor));
                sb.Append(" rot=").Append(Fmt(t.localEulerAngles));
                sb.Append(" pos=").Append(Fmt(t.localPosition));
                sb.Append(" scl=").Append(t.localScale.x.ToString("0.##"));
                sb.AppendLine();
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string Fmt(Vector3 v)
    {
        return $"({v.x:0},{v.y:0},{v.z:0})".PadRight(16);
    }

    private static string Hex(ParticleSystem.MinMaxGradient gradient)
    {
        Color color = gradient.mode == ParticleSystemGradientMode.TwoColors ? gradient.colorMax : gradient.color;
        return ColorUtility.ToHtmlStringRGB(color);
    }
}
