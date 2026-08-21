using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpHierarchy
{
    public static string Execute()
    {
        var sb = new StringBuilder();

        foreach (string path in new[]
        {
            "Assets/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Dust_Fire.prefab",
            "Assets/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_GroundCrack_Blue.prefab"
        })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            sb.AppendLine("== " + root.name);
            Walk(root.transform, 1, sb);
        }

        return sb.ToString();
    }

    private static void Walk(Transform t, int depth, StringBuilder sb)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform child = t.GetChild(i);
            sb.Append(new string(' ', depth * 3)).Append(child.name).AppendLine();
            Walk(child, depth + 1, sb);
        }
    }
}
