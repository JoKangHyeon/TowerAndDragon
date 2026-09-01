using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;

// 전장의 안개 - Hidden(시야 밖) 셀을 불투명 구름 타일로 완전히 가린다.
// 청크가 임의 모양이므로 청크 하나를 스프라이트 한 장으로 덮을 수 없다(정사각형일 때만 가능했던 방식이다) -
// 셀마다 한 칸짜리 구름 타일을 깔아 어떤 모양이든 빈틈없이 덮는다.
// Visible(시야 안·미점령)/Conquered 셀의 밝기는 FogOfWarRenderer(지형 틴트)가 그대로 담당한다.
[RequireComponent(typeof(GridMap))]
public class FogCloudRenderer : MonoBehaviour
{
    // [임시 계측] 밤→낮 전환 프리즈 조사용. CycleManager.cs의 TND. 명명 규칙을 그대로 따른다.
    private const string CELL_PAINT_MARKER_NAME = "TND.Cloud.CellPaint";
    private const string PAINT_ALL_MARKER_NAME = "TND.Cloud.PaintAll";
    private static readonly ProfilerMarker CELL_PAINT_MARKER = new(CELL_PAINT_MARKER_NAME);
    private static readonly ProfilerMarker PAINT_ALL_MARKER = new(PAINT_ALL_MARKER_NAME);

    [Tooltip("구름 타일을 그릴 타일맵 - 정렬 레이어 Fog, 지형 타일맵과 같은 Grid 아래 자식이어야 한다.")]
    [SerializeField]
    private Tilemap _cloudTilemap;

    [Tooltip("낮에 표시할 구름 타일 - 셀 한 칸을 덮는 크기여야 한다.")]
    [SerializeField]
    private TileBase _dayCloudTile;

    [Tooltip("밤에 표시할 구름 타일 - 셀 한 칸을 덮는 크기여야 한다.")]
    [SerializeField]
    private TileBase _nightCloudTile;

    // CycleManager는 Grid.prefab을 쓰는 씬(다른 팀원 테스트 씬 등)에 항상 있는 게 아니므로,
    // 없는 씬에서는 항상 낮 구름으로 고정한다(ConquestManager와 동일한 이유).
    [SerializeField]
    private CycleManager _cycleManager;

    private GridMap _gridMap;

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
        AlignToTerrainTilemap();
    }

    // 같은 Grid 아래 있어도 타일맵 오브젝트의 위치는 서로 다를 수 있다 - 셀 좌표가 같아도 두 타일맵의
    // 원점이 다르면 그려지는 자리가 그 차이만큼 통째로 어긋난다.
    // (실제로 씬의 지형 타일맵은 (-0.122, 0.067)만큼 옮겨져 있는데 구름 타일맵은 원점에 있어서,
    //  구름이 지형보다 오른쪽·아래로 밀려 그려졌다 - 서쪽 시야가 한 뼘 짧아 보이던 원인이다.)
    // 구름은 지형을 덮는 것이 전부이므로 위치를 지형 타일맵에 맞춰, 에디터에서 누가 어느 쪽을 옮기든
    // 어긋나지 않게 한다.
    private void AlignToTerrainTilemap()
    {
        Tilemap terrainTilemap = _gridMap.TerrainTilemap;
        if (terrainTilemap == null)
            return;

        Vector3 alignedPosition = terrainTilemap.transform.position;

        // z는 렌더 정렬에 쓰이므로 구름 타일맵이 가진 값을 그대로 둔다.
        alignedPosition.z = _cloudTilemap.transform.position.z;
        _cloudTilemap.transform.position = alignedPosition;
    }

    private void OnEnable()
    {
        if (_cycleManager != null)
            _cycleManager.OnCycleChanged.AddListener(HandleCycleChanged);
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
            _cycleManager.OnCycleChanged.RemoveListener(HandleCycleChanged);
    }

    // GridMap.Awake()가 셀/청크 생성을 끝내고, Castle.SetUpInitialTerritory() 등 다른 스크립트의
    // Start()가 초기 시야를 세팅한 뒤에 칠해야 한다 - FogOfWarRenderer와 동일한 이유로
    // 한 프레임 뒤로 미뤄 초기 도색한다.
    private void Start()
    {
        PaintAllCellsNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid PaintAllCellsNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        PaintAllCells();
        _gridMap.OnCellChanged.AddListener(HandleCellChanged);
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
    }

    // 청크 하나가 열리면 이 이벤트가 청크 크기만큼 연달아 오고, 밤 정산에서는 주변 청크까지 합쳐
    // 수백~수천 번 온다 - 좌표만 모아 두고 프레임당 SetTiles 한 번으로 처리한다(개별 SetTile을
    // 그만큼 부르면 타일맵이 매번 갱신 범위를 다시 계산한다).
    private readonly HashSet<Vector3Int> _dirtyCoords = new();
    private bool _isCellPaintQueued;

    private void HandleCellChanged(GridCell cell)
    {
        _dirtyCoords.Add(cell.Coord);
        QueueCellPaint();
    }

    private void QueueCellPaint()
    {
        if (_isCellPaintQueued)
            return;

        _isCellPaintQueued = true;
        CellPaintQueuedAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    // Defines.FOG_REPAINT_COALESCE_TIMING(렌더링 직전)에서 처리한다 - FogOfWarRenderer·
    // PropFogTintController와 같은 타이밍을 써야 안개·구름·장식물 밝기가 같은 프레임에 함께 바뀐다.
    private async UniTaskVoid CellPaintQueuedAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(Defines.FOG_REPAINT_COALESCE_TIMING, cancellationToken);

        _isCellPaintQueued = false;
        FlushDirtyCoords();
    }

    // 같은 좌표가 한 프레임에 여러 번 바뀌어도 중복 없이 한 번만 담기도록 HashSet에 모았다가,
    // 개별 SetTile 대신 SetTiles 한 번으로 넘긴다.
    private void FlushDirtyCoords()
    {
        if (_dirtyCoords.Count == 0)
            return;

        using (CELL_PAINT_MARKER.Auto())
        {
            var coords = new Vector3Int[_dirtyCoords.Count];
            var tiles = new TileBase[_dirtyCoords.Count];
            TileBase cloudTile = CurrentCloudTile;

            int i = 0;
            foreach (Vector3Int coord in _dirtyCoords)
            {
                coords[i] = coord;

                // 타일은 이벤트가 들어온 시점이 아니라 지금 다시 조회한다 - 같은 프레임에 같은 좌표가
                // 두 번 바뀔 수 있어, 이벤트 시점 상태를 저장해 두면 최신 상태를 놓친다.
                tiles[i] = _gridMap.GetCellState(coord) == ChunkState.Hidden ? cloudTile : null;
                i++;
            }

            _dirtyCoords.Clear();
            _cloudTilemap.SetTiles(coords, tiles);
        }
    }

    // 낮/밤이 바뀌면 현재 Hidden인 셀 전체를 다른 구름 타일로 다시 칠한다.
    private void HandleCycleChanged(CycleManager.CycleState _) => PaintAllCells();

    // 셀 집합은 GridMap.Awake 이후 바뀌지 않으므로 좌표 배열은 최초 1회만 만들고, 타일 배열은
    // 재사용한다 - 낮밤 전환마다 List를 새로 키우고 ToArray로 복사하면 맵 전체 크기의 할당이
    // 호출마다 두 번씩 생긴다.
    private Vector3Int[] _allCoords;
    private TileBase[] _cloudTileBuffer;

    private void EnsurePaintBuffers()
    {
        if (_allCoords != null)
            return;

        Dictionary<Vector3Int, GridCell>.KeyCollection allCoords = _gridMap.EnumerateAllCoords();
        _allCoords = new Vector3Int[allCoords.Count];
        allCoords.CopyTo(_allCoords, 0);
        _cloudTileBuffer = new TileBase[_allCoords.Length];
    }

    // 맵 전체를 훑을 때는 SetTile을 셀마다 부르지 않고 배열로 모아 한 번에 넘긴다 -
    // 타일맵이 갱신 범위를 한 번만 다시 계산하므로 초기 도색·낮밤 전환의 프레임 스파이크가 줄어든다.
    private void PaintAllCells()
    {
        using (PAINT_ALL_MARKER.Auto())
        {
            EnsurePaintBuffers();

            TileBase cloudTile = CurrentCloudTile;

            for (int i = 0; i < _allCoords.Length; i++)
            {
                _cloudTileBuffer[i] = _gridMap.GetCellState(_allCoords[i]) == ChunkState.Hidden ? cloudTile : null;
            }

            _cloudTilemap.SetTiles(_allCoords, _cloudTileBuffer);
        }
    }

    private TileBase CurrentCloudTile =>
        _cycleManager != null && _cycleManager.CurrentCycle == CycleManager.CycleState.Night
            ? _nightCloudTile
            : _dayCloudTile;
}
