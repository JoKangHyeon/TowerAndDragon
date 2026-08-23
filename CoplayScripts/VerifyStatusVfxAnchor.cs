using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MonsterStatusVfx.Bind의 렌더러 해석을 몬스터 프리팹 전종에 대해 그대로 돌려 본다.
///
/// 확인하려는 것: 몸통이 아닌 스프라이트(방어막·오버레이 등)가 잡히는 몬스터가 있는지.
/// GetComponentInChildren는 비활성 자식까지 보므로, 꺼진 채 저장된 오버레이가 이길 수 있다.
/// </summary>
public static class VerifyStatusVfxAnchor
{
    private const string PREFAB_FOLDER = "Assets/Data/MonsterData/MonsterPrefab";

    public static void Execute()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_FOLDER });
        var report = new StringBuilder();
        var suspects = new List<string>();

        report.AppendLine($"[VerifyStatusVfxAnchor] 프리팹 {guids.Length}개");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                continue;
            }

            Transform owner = prefab.transform;

            // MonsterStatusVfx.Bind와 같은 순서.
            SpriteRenderer resolved = owner.GetComponent<SpriteRenderer>();
            string branch = "root";

            if (resolved == null)
            {
                resolved = owner.GetComponentInChildren<SpriteRenderer>(true);
                branch = "child";
            }

            if (resolved == null)
            {
                report.AppendLine($"  {prefab.name}: 스프라이트 없음 → 대용 높이 사용");
                suspects.Add($"{prefab.name} (스프라이트 없음)");
                continue;
            }

            string renderPath = GetPath(resolved.transform, owner);
            bool isActive = resolved.gameObject.activeSelf;
            string sprite = resolved.sprite != null ? resolved.sprite.name : "<스프라이트 미지정>";

            report.AppendLine(
                $"  {prefab.name}: [{branch}] {renderPath} " +
                $"active={isActive} sprite={sprite} scale={owner.localScale.x}");

            // 루트가 아니면서 꺼져 있거나 스프라이트가 없으면 몸통이 아닐 가능성이 높다.
            if (branch == "child" && (!isActive || resolved.sprite == null))
            {
                suspects.Add($"{prefab.name} → {renderPath} (active={isActive}, sprite={sprite})");
            }
        }

        report.AppendLine();

        if (suspects.Count == 0)
        {
            report.AppendLine("의심 항목 없음 - 전부 몸통 스프라이트로 해석됨.");
        }
        else
        {
            report.AppendLine($"의심 {suspects.Count}건:");

            foreach (string suspect in suspects)
            {
                report.AppendLine($"  ! {suspect}");
            }
        }

        Debug.Log(report.ToString());
    }

    private static string GetPath(Transform target, Transform root)
    {
        if (target == root)
        {
            return "<루트>";
        }

        var parts = new List<string>();

        for (Transform current = target; current != null && current != root; current = current.parent)
        {
            parts.Insert(0, current.name);
        }

        return string.Join("/", parts);
    }
}
