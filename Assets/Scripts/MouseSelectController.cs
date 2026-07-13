using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MouseSelectController : MonoBehaviour
{
    private const int FOOTPRINT_CENTER_DIVISOR = 2;

    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private float _yOffset = 0.7f;

    [SerializeField]
    private SpriteRenderer _ghostRenderer;

    [SerializeField]
    private float _ghostAlpha = 0.5f;

    [SerializeField]
    private Color _ghostBlockedTint = new Color(1f, 0.4f, 0.4f);

    [SerializeField]
    private Color _selectionHighlightColor = Color.yellow;

    [SerializeField]
    private Color _occupiedOverlayColor = new Color(1f, 0f, 0f, 0.35f);

    private Camera _cam;
    private readonly List<SpriteRenderer> _highlightPool = new();
    private readonly List<SpriteRenderer> _occupiedOverlayPool = new();
    private FootprintShape _footprintShape = new FootprintShape(new bool[1, 1] { { true } });
    private Vector3 _ghostLocalOffset;
    private bool _isPlacementActive;
    private Building _selectedBuildingRef; // 재배치 중이면 실제 인스턴스 - 자기 자신과 겹치는 위치도 유효하게 판정하기 위함

    public Vector3Int CurrentAnchor { get; private set; }
    public bool CanConstruct { get; private set; }
    public Color SelectionHighlightColor => _selectionHighlightColor;
    public float YOffset => _yOffset;


    private void Awake()
    {
        _cam = Camera.main;
        _highlightPool.Add(_spriteRenderer);
        Deactivate();
    }

    public void SetPlacementActive(bool isActive)
    {
        _isPlacementActive = isActive;

        if (!_isPlacementActive)
            Deactivate();
    }

    public Vector3Int GetHoveredCell()
    {
        Vector3 worldPos = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;
        worldPos.y -= _yOffset;

        return _gridMap.ConvertWorldToGrid(worldPos);
    }

    public Vector3Int GetHoveredAnchor(FootprintShape shape) => GetFootprintAnchor(GetHoveredCell(), shape);

    private void Update()
    {
        if (!_isPlacementActive)
            return;

        Vector3Int hoveredCell = GetHoveredCell();
        Vector3Int anchor = GetFootprintAnchor(hoveredCell, _footprintShape);
        List<Vector3Int> footprint = _gridMap.GetFootprintCoords(anchor, _footprintShape);
        bool canConstruct = _gridMap.CanConstructFootPrint(anchor, _footprintShape, _selectedBuildingRef);

        CurrentAnchor = anchor;
        CanConstruct = canConstruct;

        DrawFootprint(footprint, canConstruct);
        DrawGhost(anchor, canConstruct);
    }

    public void BeginPlacementPreview(Building prefab) => SetPreviewTarget(prefab);
    public void BeginRepositionPreview(Building building) => SetPreviewTarget(building);

    private void SetPreviewTarget(Building building)
    {
        _selectedBuildingRef = building;
        _footprintShape = building.FootprintShape;
        _ghostLocalOffset = building.PlacementOffset;

        if (_ghostRenderer == null)
            return;

        SpriteRenderer prefabRenderer = building.GetComponent<SpriteRenderer>();
        Sprite ghostSprite = prefabRenderer != null ? prefabRenderer.sprite : null;

        _ghostRenderer.sprite = ghostSprite;
        _ghostRenderer.transform.localScale = building.transform.localScale;
        _ghostRenderer.gameObject.SetActive(ghostSprite != null);
    }

    private Vector3Int GetFootprintAnchor(Vector3Int hoveredCell, FootprintShape shape)
    {
        int offsetX = (shape.Width - 1) / FOOTPRINT_CENTER_DIVISOR;
        int offsetY = (shape.Height - 1) / FOOTPRINT_CENTER_DIVISOR;
        return hoveredCell - new Vector3Int(offsetX, offsetY, 0);
    }

    private void DrawFootprint(List<Vector3Int> footprint, bool canConstruct)
    {
        Color highlightColor = canConstruct ? Color.green : Color.red;
        HighlightCells(footprint, highlightColor);
    }

    public void HighlightCells(List<Vector3Int> coords, Color color) => HighlightCells(coords, color, _highlightPool);

    private void HighlightCells(List<Vector3Int> coords, Color color, List<SpriteRenderer> pool)
    {
        for (int i = 0; i < coords.Count; i++)
        {
            SpriteRenderer highlight = GetPooledHighlight(i, pool);
            Vector3 cellPos = _gridMap.ConvertGridToWorld(coords[i]);
            cellPos.y += _yOffset;
            highlight.transform.position = cellPos;
            highlight.color = color;
        }

        for (int i = coords.Count; i < pool.Count; i++)
        {
            pool[i].gameObject.SetActive(false);
        }
    }

    public void HighlightSelection(List<Vector3Int> coords) => HighlightCells(coords, _selectionHighlightColor);

    // 건설 모드에서 이미 건물이 배치된 타일을 표시 - 어떤 땅이 비어있는지 한눈에 파악 가능
    public void ShowOccupiedOverlay(List<Vector3Int> coords) => HighlightCells(coords, _occupiedOverlayColor, _occupiedOverlayPool);

    public void ClearOccupiedOverlay() => ClearHighlights(_occupiedOverlayPool);

    public void ClearHighlights() => ClearHighlights(_highlightPool);

    private void ClearHighlights(List<SpriteRenderer> pool)
    {
        foreach (SpriteRenderer highlight in pool)
        {
            highlight.gameObject.SetActive(false);
        }
    }

    private void DrawGhost(Vector3Int anchor, bool canConstruct)
    {
        if (_ghostRenderer == null || !_ghostRenderer.gameObject.activeSelf)
            return;

        _ghostRenderer.transform.position = _gridMap.GetFootprintCenterWorld(anchor, _footprintShape) + _ghostLocalOffset;

        Color color = canConstruct ? Color.white : _ghostBlockedTint;
        color.a = _ghostAlpha;
        _ghostRenderer.color = color;
    }

    private void Deactivate()
    {
        if (_ghostRenderer != null)
            _ghostRenderer.gameObject.SetActive(false);

        ClearHighlights();
        CanConstruct = false;
    }

    private SpriteRenderer GetPooledHighlight(int index, List<SpriteRenderer> pool)
    {
        if (index >= pool.Count)
        {
            pool.Add(Instantiate(_spriteRenderer, transform));
        }

        SpriteRenderer pooled = pool[index];
        pooled.gameObject.SetActive(true);
        return pooled;
    }
}
