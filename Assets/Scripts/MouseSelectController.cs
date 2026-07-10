using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MouseSelectController : MonoBehaviour
{
    private const int FOOTPRINT_CENTER_DIVISOR = 2;
    private const int DEFAULT_CELL_SIZE = 1;

    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private float _yOffset = 0.7f;

    private Camera _cam;
    private int _cellSize = DEFAULT_CELL_SIZE;
    private readonly List<SpriteRenderer> _highlightPool = new();

    public Vector3Int CurrentAnchor { get; private set; }
    public bool CanConstruct { get; private set; }


    private void Awake()
    {
        _cam = Camera.main;
        _highlightPool.Add(_spriteRenderer);
    }

    private void Update()
    {
        Vector3 worldPos = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;
        worldPos.y -= _yOffset;

        Vector3Int hoveredCell = _gridMap.ConvertWorldToGrid(worldPos);
        Vector3Int anchor = GetFootprintAnchor(hoveredCell, _cellSize);
        List<Vector3Int> footprint = _gridMap.GetFootprintCoords(anchor, _cellSize);
        bool canConstruct = _gridMap.CanConstructFootPrint(anchor, _cellSize);

        CurrentAnchor = anchor;
        CanConstruct = canConstruct;

        DrawFootprint(footprint, canConstruct);
    }
    public void SetCellSize(int cellSize)
    {
        _cellSize = Mathf.Max(DEFAULT_CELL_SIZE, cellSize);
    }

    private Vector3Int GetFootprintAnchor(Vector3Int hoveredCell, int cellSize)
    {
        int offset = (cellSize - 1) / FOOTPRINT_CENTER_DIVISOR;
        return hoveredCell - new Vector3Int(offset, offset, 0);
    }

    private void DrawFootprint(List<Vector3Int> footprint, bool canConstruct)
    {
        Color highlightColor = canConstruct ? Color.green : Color.red;

        for (int i = 0; i < footprint.Count; i++)
        {
            SpriteRenderer highlight = GetPooledHighlight(i);
            Vector3 cellPos = _gridMap.ConvertGridToWorld(footprint[i]);
            cellPos.y += _yOffset;
            highlight.transform.position = cellPos;
            highlight.color = highlightColor;
        }

        for (int i = footprint.Count; i < _highlightPool.Count; i++)
        {
            _highlightPool[i].gameObject.SetActive(false);
        }
    }

    private SpriteRenderer GetPooledHighlight(int index)
    {
        if (index >= _highlightPool.Count)
        {
            _highlightPool.Add(Instantiate(_spriteRenderer, transform));
        }

        SpriteRenderer pooled = _highlightPool[index];
        pooled.gameObject.SetActive(true);
        return pooled;
    }
}
