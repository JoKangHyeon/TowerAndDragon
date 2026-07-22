using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class FootprintShape
{
    private const int CENTER_OFFSET_DIVISOR = 2;

    // 90도 단위 회전 - 0/90/180/270도, 4번 돌면 원래 모양으로 돌아온다.
    public const int ROTATION_STEP_COUNT = 4;

    // 짝수 크기 축은 진짜 기하학적 중심이 정수 칸이 아니라 칸과 칸 사이(경계)에 있다 - 그 어긋난 만큼(반 칸).
    private const float PARITY_MISMATCH_HALF_CELL = 0.5f;
    private const float PARITY_MISMATCH_NONE = 0f;

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

    // 진짜 기하학적 중심과 CenterOffset(정수 칸) 사이의 어긋남 - 축의 크기가 짝수면 0.5, 홀수면 0(그리드 칸 단위).
    // 회전 전후로 가로/세로가 서로 바뀌므로, 이 값의 변화량이 곧 회전 시 시각적으로 밀리는 정도가 된다.
    public Vector2 ParityMismatch =>
        new Vector2(
            _width % 2 == 0 ? PARITY_MISMATCH_HALF_CELL : PARITY_MISMATCH_NONE,
            _height % 2 == 0 ? PARITY_MISMATCH_HALF_CELL : PARITY_MISMATCH_NONE);

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

    // steps만큼 90도 단위로 돌린 새 모양을 반환한다(원본은 그대로 둠 - 프리팹 에셋에 안전하게 적용 가능).
    // 음수나 4 이상의 값도 0~3 범위로 자동 정규화된다.
    public FootprintShape Rotated(int steps)
    {
        int normalizedSteps = ((steps % ROTATION_STEP_COUNT) + ROTATION_STEP_COUNT) % ROTATION_STEP_COUNT;

        bool[,] grid = ToGrid();
        for (int i = 0; i < normalizedSteps; i++)
            grid = RotatedOnce(grid);

        return new FootprintShape(grid);
    }

    private bool[,] ToGrid()
    {
        bool[,] grid = new bool[_width, _height];
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
                grid[x, y] = IsOccupied(x, y);
        }
        return grid;
    }

    // (x, y) -> (y, width-1-x) 매핑으로 90도 회전 - 가로/세로 크기가 서로 바뀐다.
    private static bool[,] RotatedOnce(bool[,] grid)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        bool[,] rotated = new bool[height, width];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                rotated[y, width - 1 - x] = grid[x, y];
        }

        return rotated;
    }
}

