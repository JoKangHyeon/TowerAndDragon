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

    private Camera _cam;
    private readonly List<SpriteRenderer> _highlightPool = new();
    private FootprintShape _footprintShape = new FootprintShape(new bool[1, 1] { { true } });
    private Vector3 _ghostLocalOffset;

    public Vector3Int CurrentAnchor { get; private set; }
    public bool CanConstruct { get; private set; }


    private void Awake()
    {
        _cam = Camera.main;
        _highlightPool.Add(_spriteRenderer);

        if (_ghostRenderer != null)
            _ghostRenderer.gameObject.SetActive(false);
    }

    private void Update()
    {
        Vector3 worldPos = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;
        worldPos.y -= _yOffset;

        Vector3Int hoveredCell = _gridMap.ConvertWorldToGrid(worldPos);
        Vector3Int anchor = GetFootprintAnchor(hoveredCell, _footprintShape);
        List<Vector3Int> footprint = _gridMap.GetFootprintCoords(anchor, _footprintShape);
        bool canConstruct = _gridMap.CanConstructFootPrint(anchor, _footprintShape);

        CurrentAnchor = anchor;
        CanConstruct = canConstruct;

        DrawFootprint(footprint, canConstruct);
        DrawGhost(anchor, canConstruct);
    }

    public void SetSelectedBuilding(Building prefab)
    {
        _footprintShape = prefab.FootprintShape;
        _ghostLocalOffset = prefab.transform.localPosition;

        if (_ghostRenderer == null)
            return;

        SpriteRenderer prefabRenderer = prefab.GetComponent<SpriteRenderer>();
        Sprite ghostSprite = prefabRenderer != null ? prefabRenderer.sprite : null;

        _ghostRenderer.sprite = ghostSprite;
        _ghostRenderer.transform.localScale = prefab.transform.localScale;
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

    private void DrawGhost(Vector3Int anchor, bool canConstruct)
    {
        if (_ghostRenderer == null || !_ghostRenderer.gameObject.activeSelf)
            return;

        _ghostRenderer.transform.position = _gridMap.GetFootprintCenterWorld(anchor, _footprintShape) + _ghostLocalOffset;

        Color color = canConstruct ? Color.white : _ghostBlockedTint;
        color.a = _ghostAlpha;
        _ghostRenderer.color = color;
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
