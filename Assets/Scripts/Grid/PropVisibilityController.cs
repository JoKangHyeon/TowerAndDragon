using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 건물 스프라이트와 화면상 겹치는 Props(장식물)를 숨기고, 철거되면 다시 보여준다.
// 아이소메트릭 스프라이트는 피벗과 실제 그림 영역이 어긋나므로, 그리드 셀 좌표가 아니라
// 스프라이트 바운즈(X/Y)가 겹치는지로 판단한다.
public class PropVisibilityController : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    private readonly List<SpriteRenderer> _propRenderers = new();

    // 건물 바운즈 재사용 버퍼 - 갱신마다 새 리스트를 만들지 않는다.
    private readonly List<Bounds> _buildingBoundsBuffer = new();

    private bool _isRefreshQueued;

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

    private void HandleCellChanged(GridCell cell) => QueueRefresh();

    // 청크 하나가 열리면 셀 이벤트가 청크 크기만큼 연달아 오고, 초기 영토 세팅에서는 수백 번 온다 -
    // 실제 재계산은 프레임당 한 번만 한다.
    private void QueueRefresh()
    {
        if (_isRefreshQueued)
            return;

        _isRefreshQueued = true;
        RefreshNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RefreshNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        _isRefreshQueued = false;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        _buildingBoundsBuffer.Clear();

        foreach (Building building in _gridMap.Buildings)
        {
            if (building.TryGetComponent(out SpriteRenderer buildingRenderer))
                _buildingBoundsBuffer.Add(buildingRenderer.bounds);
        }

        foreach (SpriteRenderer propRenderer in _propRenderers)
        {
            propRenderer.gameObject.SetActive(!IsCoveredByBuilding(propRenderer.bounds));
        }
    }

    private bool IsCoveredByBuilding(Bounds propBounds)
    {
        foreach (Bounds buildingBounds in _buildingBoundsBuffer)
        {
            if (OverlapsXY(buildingBounds, propBounds))
                return true;
        }

        return false;
    }

    // 아이소메트릭 정렬을 위해 z 위치가 서로 달라 3D Bounds.Intersects는 항상 실패하므로, 화면상 축인 X/Y만 비교한다.
    private static bool OverlapsXY(Bounds a, Bounds b) =>
        a.min.x < b.max.x && a.max.x > b.min.x &&
        a.min.y < b.max.y && a.max.y > b.min.y;
}
