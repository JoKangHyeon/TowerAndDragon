using System.Text;
using UnityEditor;
using UnityEngine;

// 메시 파티클이 "어디서부터 솟는지"는 좌표가 아니라 <b>회전·크기까지 먹인 메시 바닥</b>이 정한다.
// 같은 y에 놓아도 눕힌 각도가 다르면 한쪽만 지면을 뚫고 내려간다.
public static class MeasureRockBottoms
{
    public static string Execute()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Imported/Prefabs/Effects/Status/FX_Impact_StoneCrack.prefab");

        var sb = new StringBuilder();

        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            string n = ps.gameObject.name;

            if (!n.StartsWith("rock") && n != "Mountain")
            {
                continue;
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();

            if (renderer == null || renderer.mesh == null)
            {
                continue;
            }

            Bounds bounds = renderer.mesh.bounds;
            float startSize = ps.main.startSize.constant;
            Vector3 scale = ps.transform.localScale * startSize;
            Quaternion rotation = ps.transform.localRotation;

            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);

                float y = (rotation * Vector3.Scale(local, scale)).y;
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
            }

            float pivotY = ps.transform.localPosition.y;

            sb.Append(n.PadRight(12));
            sb.Append(" mesh=").Append(renderer.mesh.name.PadRight(12));
            sb.Append(" 기준점y=").Append(pivotY.ToString("0.###").PadRight(9));
            sb.Append(" 바닥y=").Append((pivotY + minY).ToString("0.###").PadRight(9));
            sb.Append(" 꼭대기y=").Append((pivotY + maxY).ToString("0.###").PadRight(9));
            sb.Append(" 세로높이=").Append((maxY - minY).ToString("0.###"));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
