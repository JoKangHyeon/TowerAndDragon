using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Props(장식물)를 자신이 선 셀의 ChunkState에 맞춰 FogOfWarRenderer와 같은 밝기로 어둡게 틴트한다.
// 장식물은 고정된 오브젝트라 몬스터(FogTintReceiver)처럼 매 프레임 확인할 필요 없이, 처음 한 번만
// 셀 좌표를 계산해 캐싱하고 GridMap.OnCellChanged(셀 상태가 바뀔 때만) 갱신한다.
public class PropFogTintController : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    private FogOfWarRenderer _fogOfWarRenderer;
    private readonly List<(SpriteRenderer Renderer, Vector3Int Coord)> _props = new();

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

        foreach (SpriteRenderer renderer in renderers)
        {
            Vector3Int coord = _gridMap.ConvertWorldToGrid(renderer.transform.position);
            _props.Add((renderer, coord));
        }
    }

    // Castle.SetUpInitialTerritory() 등이 Start()에서 초기 시야를 세팅한 뒤에 칠해야 한다 -
    // FogOfWarRenderer와 동일한 이유로 한 프레임 뒤로 미뤄 초기 도색한다.
    private void Start()
    {
        RefreshNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RefreshNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        if (_gridMap == null || _fogOfWarRenderer == null)
            return;

        RefreshAll();
        _gridMap.OnCellChanged += HandleCellChanged;
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged -= HandleCellChanged;
    }

    private void HandleCellChanged(GridCell cell) => RefreshAll();

    private void RefreshAll()
    {
        foreach ((SpriteRenderer renderer, Vector3Int coord) in _props)
        {
            renderer.color = _fogOfWarRenderer.GetTintColor(_gridMap.GetCellState(coord));
        }
    }
}
