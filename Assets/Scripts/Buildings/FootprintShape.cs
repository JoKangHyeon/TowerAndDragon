using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FootprintShape
{
    private const int CENTER_OFFSET_DIVISOR = 2;

    [SerializeField]
    private int _width = 1;
    [SerializeField]
    private int _height = 1;
    [SerializeField]
    private bool[] _cells = { true };

    public int Width => _width;
    public int Height => _height;

    // 이 모양의 중심 셀 기준으로 좌하단 앵커를 구하기 위한 오프셋.
    // 짝수 크기일 때는 정수 나눗셈으로 내림 처리된다 (예: 폭 2 -> 오프셋 0).
    public Vector3Int CenterOffset =>
        new Vector3Int((_width - 1) / CENTER_OFFSET_DIVISOR, (_height - 1) / CENTER_OFFSET_DIVISOR, 0);

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

