using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

// Props(장식물)를 자신이 선 셀의 ChunkState에 맞춰 FogOfWarRenderer와 같은 밝기로 어둡게 틴트한다.
// 장식물은 고정된 오브젝트라 몬스터(FogTintReceiver)처럼 매 프레임 확인할 필요 없이, 처음 한 번만
// 셀 좌표를 계산해 캐싱하고 GridMap.OnCellChanged(셀 상태가 바뀔 때만) 갱신한다.
public class PropFogTintController : MonoBehaviour
{
    // [임시 계측] 밤→낮 전환 프리즈 조사용. CycleManager.cs의 TND. 명명 규칙을 그대로 따른다.
    private const string REFRESH_ALL_MARKER_NAME = "TND.PropFogTint.RefreshAll";
    private static readonly ProfilerMarker REFRESH_ALL_MARKER = new(REFRESH_ALL_MARKER_NAME);

    [SerializeField]
    private GridMap _gridMap;

    private FogOfWarRenderer _fogOfWarRenderer;
    private readonly List<(SpriteRenderer Renderer, Vector3Int Coord)> _props = new();

    private bool _isRefreshQueued;

    private void Awake()
    {
        if (_gridMap == null)
        {
            Debug.LogWarning($"[PropFogTintController] GridMap 인스펙터 연결 필요");
            return;
        }

        _fogOfWarRenderer = _gridMap.GetComponent<FogOfWarRenderer>();

        var renderers = new List<SpriteRenderer>();
        GetComponentsInChildren(true, renderers);

        // 장식물은 렌더된 지형 위에 직접 놓여 있어 단차만큼 올라간 Y를 그대로 갖는다. 평면 역변환
        // (ConvertWorldToGrid)으로는 그 Y를 되돌리지 못해 언덕 위 장식물이 이웃 셀로 잡히므로,
        // 고저차를 감안해 실제로 그 지점을 덮고 있는 타일을 고르는 PickCellAtWorldPoint를 쓴다.
        foreach (SpriteRenderer renderer in renderers)
        {
            Vector3Int coord = _gridMap.PickCellAtWorldPoint(renderer.transform.position);
            _props.Add((renderer, coord));
        }
    }

    // Castle.SetUpInitialTerritory() 등이 Start()에서 초기 시야를 세팅한 뒤에 칠해야 한다 -
    // FogOfWarRenderer와 동일한 이유로 한 프레임 뒤로 미뤄 초기 도색한다.
    private void Start()
    {
        InitializeNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid InitializeNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        if (_gridMap == null || _fogOfWarRenderer == null)
            return;

        RefreshAll();
        _gridMap.OnCellChanged.AddListener(HandleCellChanged);
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
    }

    private void HandleCellChanged(GridCell cell) => QueueRefresh();

    // 청크 하나가 열리면 셀 이벤트가 청크 크기만큼 연달아 오고, 밤 정산에서는 주변 청크까지 합쳐
    // 수백~수천 번 온다 - 실제 도색은 프레임당 한 번만 한다(PropVisibilityController.QueueRefresh와
    // 같은 이유). Defines.FOG_REPAINT_COALESCE_TIMING(렌더링 직전)에서 처리해, 안개·구름과 한 프레임도
    // 어긋나지 않게 한다.
    private void QueueRefresh()
    {
        if (_isRefreshQueued)
            return;

        _isRefreshQueued = true;
        RefreshQueuedAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RefreshQueuedAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(Defines.FOG_REPAINT_COALESCE_TIMING, cancellationToken);

        _isRefreshQueued = false;
        RefreshAll();
    }

    private void RefreshAll()
    {
        using (REFRESH_ALL_MARKER.Auto())
        {
            foreach ((SpriteRenderer renderer, Vector3Int coord) in _props)
            {
                renderer.color = _fogOfWarRenderer.GetTintColor(_gridMap.GetCellState(coord));
            }
        }
    }
}
