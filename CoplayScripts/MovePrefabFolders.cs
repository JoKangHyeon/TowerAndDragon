using System.Text;
using UnityEditor;

// Assets/Prefabs/{Effects,Projectile} -> Assets/Imported/Prefabs/{Effects,Projectile}
//
// AssetDatabase.MoveAsset을 쓰는 이유: .meta를 함께 옮겨 GUID를 보존한다.
// 탐색기로 옮기면 .meta가 남거나 빠져 GUID가 재발급되고, TowerData·ProjectileVisual의
// 참조가 통째로 끊긴다(이 프로젝트에서 전에 같은 사고가 있었다).
public static class MovePrefabFolders
{
    private const string SOURCE_ROOT = "Assets/Prefabs";
    private const string TARGET_ROOT = "Assets/Imported/Prefabs";

    private static readonly string[] FOLDERS = { "Effects", "Projectile" };

    public static string Move()
    {
        var report = new StringBuilder();

        EnsureFolder("Assets/Imported", "Prefabs");

        foreach (string folder in FOLDERS)
        {
            string source = $"{SOURCE_ROOT}/{folder}";
            string target = $"{TARGET_ROOT}/{folder}";

            if (!AssetDatabase.IsValidFolder(source))
            {
                report.AppendLine($"건너뜀(원본 없음): {source}");
                continue;
            }

            if (AssetDatabase.IsValidFolder(target))
            {
                report.AppendLine($"건너뜀(대상이 이미 있음): {target}");
                continue;
            }

            string error = AssetDatabase.MoveAsset(source, target);

            report.AppendLine(string.IsNullOrEmpty(error)
                ? $"이동: {source} -> {target}"
                : $"실패: {source} -> {target} ({error})");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return report.ToString();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
