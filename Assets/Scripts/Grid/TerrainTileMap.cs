using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "New Terrain Tile Map", menuName = "Grid/Terrain Tile Map")]
public class TerrainTileMap : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public TileBase Tile;
        public TerrainType TerrainType;
        public bool CanConstruct;
    }

    [SerializeField] private Entry[] _entries;

    public TerrainType Resolve(TileBase tile)
    {
        foreach (var entry in _entries)
        {
            if (entry.Tile == tile)
                return entry.TerrainType;
        }

        return TerrainType.Default;
    }
}
