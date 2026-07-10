using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FootprintShape
{
    [SerializeField] 
    private int _width = 1;
    [SerializeField] 
    private int _height = 1;
    [SerializeField] 
    private bool[] _cells = { true }; 

    public int Width => _width;
    public int Height => _height;

    public FootprintShape(bool[,] shape)
    {
        _width = shape.GetLength(0);
        _height = shape.GetLength(1);
        _cells = new bool[_width * _height];
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                _cells[y * _width + x] = shape[x, y];
            }
        }
    }
    public bool IsOccupied(int x, int y) => _cells[y * _width + x];

    public IEnumerable<Vector2Int> GetOccupiedOffsets()
    {
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                if (IsOccupied(x, y))
                    yield return new Vector2Int(x, y);
            }
        }
    }
}

