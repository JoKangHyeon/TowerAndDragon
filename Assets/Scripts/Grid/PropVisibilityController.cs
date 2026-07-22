using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 건물 스프라이트와 화면상 겹치는 Props(장식물)를 숨기고, 철거되면 다시 보여준다.
// 아이소메트릭 스프라이트는 피벗과 실제 그림 영역이 어긋나므로, 그리드 셀 좌표가 아니라
// 스프라이트 바운즈(X/Y)가 겹치는지로 판단한다.
public class PropVisibilityController : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    private readonly List<SpriteRenderer> _propRenderers = new();

    private void Awake()
    {
        if (_gridMap == null)
        {
            Debug.LogWarning($"[PropVisibilityController] GridMap 인스펙터 연결 필요");
            return;
        }

        GetComponentsInChildren(true, _propRenderers);
        _gridMap.OnCellChanged.AddListener(HandleCellChanged);
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
    }

    private void HandleCellChanged(GridCell cell) => RefreshVisibility();

    private void RefreshVisibility()
    {
        List<Bounds> buildingBounds = _gridMap.GetAllOccupiedCoords()
            .Select(coord => _gridMap.GetBuildingAt(coord))
            .Distinct()
            .Select(building => building.GetComponent<SpriteRenderer>())
            .Where(renderer => renderer != null)
            .Select(renderer => renderer.bounds)
            .ToList();

        foreach (SpriteRenderer propRenderer in _propRenderers)
        {
            bool isCovered = buildingBounds.Any(bounds => OverlapsXY(bounds, propRenderer.bounds));
            propRenderer.gameObject.SetActive(!isCovered);
        }
    }

    // 아이소메트릭 정렬을 위해 z 위치가 서로 달라 3D Bounds.Intersects는 항상 실패하므로, 화면상 축인 X/Y만 비교한다.
    private static bool OverlapsXY(Bounds a, Bounds b) =>
        a.min.x < b.max.x && a.max.x > b.min.x &&
        a.min.y < b.max.y && a.max.y > b.min.y;
}
