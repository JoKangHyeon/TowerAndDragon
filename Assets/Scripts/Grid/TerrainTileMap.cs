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

        [Tooltip("이 타일의 셀이 기본으로 갖는 자원 생산 플래그(복수 선택 가능). 예: Grass = Food | Wood")]
        public ResourceType DefaultResourceNodes;
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

    public bool ResolveCanConstruct(TileBase tile)
    {
        foreach (var entry in _entries)
        {
            if (entry.Tile == tile)
            {
                if (entry.CanConstruct)
                    return true;
            }
        }
        return false;
    }

    public ResourceType ResolveDefaultResourceNodes(TileBase tile)
    {
        foreach (var entry in _entries)
        {
            if (entry.Tile == tile)
                return entry.DefaultResourceNodes;
        }
        return ResourceType.None;
    }
}
