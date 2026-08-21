using System.Text;
using UnityEditor;
using UnityEngine;

public static class DumpBluerockTransforms
{
    private const string PATH =
        "Assets/Imported/Eric VFX Studio/Game VFX - Ground Crack & Explosion/Prefabs/URP/FX_Crack_RockAOE.prefab";

    public static string Execute()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PATH);
        var sb = new StringBuilder();

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            sb.Append(t.name.PadRight(18));
            sb.Append(" parent=").Append((t.parent == null ? "-" : t.parent.name).PadRight(18));
            sb.Append(" localScale=").Append(t.localScale.ToString("0.##").PadRight(20));
            sb.Append(" lossy=").Append(t.lossyScale.ToString("0.##").PadRight(20));
            sb.Append(" rot=").Append(t.localEulerAngles.ToString("0"));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
