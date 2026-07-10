using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 에디터 전용 일회성 스크립트 — 흰 낙서처럼 보이는 Cracks 오버레이 전체 제거
public static class RemoveCracks
{
    public static string Execute()
    {
        var grid = GameObject.Find("Grid");
        var decoTm = grid.transform.Find("Decoration").GetComponent<Tilemap>();
        Undo.RegisterCompleteObjectUndo(decoTm, "Remove cracks");

        decoTm.CompressBounds();
        var toRemove = new List<Vector3Int>();
        foreach (var p in decoTm.cellBounds.allPositionsWithin)
        {
            var t = decoTm.GetTile(p);
            if (t != null && t.name.StartsWith("ISO_Overlay_Cracks")) toRemove.Add(p);
        }
        foreach (var p in toRemove) decoTm.SetTile(p, null);

        EditorSceneManager.MarkSceneDirty(grid.scene);
        return $"removed cracks: {toRemove.Count}";
    }
}
