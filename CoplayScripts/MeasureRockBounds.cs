using System.Text;
using UnityEditor;
using UnityEngine;

public static class MeasureRockBounds
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Imported/Prefabs/Effects/Status/FX_Impact_IceCrack.prefab");

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null || renderer.renderMode != ParticleSystemRenderMode.Mesh || renderer.mesh == null)
            {
                continue;
            }

            Vector3 meshSize = renderer.mesh.bounds.size;
            Vector3 lossy = ps.transform.lossyScale;
            float startSize = ps.main.startSize.constant;

            // 메시 파티클의 화면 크기 = 메시 바운즈 x startSize x 계층 스케일.
            Vector3 world = new Vector3(
                meshSize.x * startSize * lossy.x,
                meshSize.y * startSize * lossy.y,
                meshSize.z * startSize * lossy.z);

            sb.Append(ps.gameObject.name.PadRight(12));
            sb.Append(" mesh=").Append(renderer.mesh.name.PadRight(14));
            sb.Append(" bounds=").Append(meshSize.ToString("0.##").PadRight(22));
            sb.Append(" startSize=").Append(startSize.ToString("0.##").PadRight(6));
            sb.Append(" lossy=").Append(lossy.ToString("0.###").PadRight(24));
            sb.Append(" 화면크기=").Append(world.ToString("0.##"));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
