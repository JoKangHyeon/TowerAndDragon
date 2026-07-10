using System.Text;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class ReportTilemapBounds
{
    public static string Execute()
    {
        var sb = new StringBuilder();
        var grid = Object.FindFirstObjectByType<Grid>();
        if (grid != null)
        {
            sb.AppendLine($"Grid cellSize={grid.cellSize} cellLayout={grid.cellLayout}");
        }

        foreach (var tm in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            tm.CompressBounds();
            var b = tm.cellBounds;
            sb.AppendLine(
                $"{tm.name}: bounds min=({b.xMin},{b.yMin}) max=({b.xMax - 1},{b.yMax - 1}) " +
                $"size={b.size.x}x{b.size.y} tileCount={tm.GetUsedTilesCount()}");
        }

        return sb.ToString();
    }
}
