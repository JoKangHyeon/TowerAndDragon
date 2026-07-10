using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 에디터 전용 일회성 스크립트 — 프랍 스케일 정규화 (성 3x3 기준, 나무 ≈ 타일 1칸 높이)
public static class ScaleProps
{
    const float MAX_HEIGHT = 0.9f; // 월드 유닛 (셀 높이 0.5의 약 1.8배)

    public static string Execute()
    {
        var sb = new StringBuilder();
        var grid = GameObject.Find("Grid");
        var props = grid.transform.Find("Props");

        int scaled = 0;
        foreach (var sr in props.GetComponentsInChildren<SpriteRenderer>(true))
        {
            float h = sr.bounds.size.y;
            if (h <= MAX_HEIGHT) continue;
            Undo.RecordObject(sr.transform, "Scale props");
            float f = MAX_HEIGHT / h * sr.transform.localScale.y;
            sr.transform.localScale = new Vector3(f, f, 1f);
            sb.AppendLine($"{sr.name}: h={h:F2} -> scale={f:F2}");
            scaled++;
        }

        EditorSceneManager.MarkSceneDirty(grid.scene);
        sb.AppendLine($"scaled={scaled}");
        return sb.ToString();
    }
}
