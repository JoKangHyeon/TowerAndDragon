using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

// 전장의 안개 - Hidden(시야 밖) 청크만 불투명 구름 스프라이트로 완전히 가린다.
// 셀 단위가 아니라 청크 단위로 그린다 - 청크가 통째로 Hidden일 때만 그 청크의 정중앙 셀에 청크 크기만큼
// 확대한 구름 타일 한 장을 배치한다(SetChunkState가 청크 내 모든 셀 상태를 동일하게 맞추므로 청크 단위
// 판정으로 충분하다). 스프라이트 개수가 줄고 큰 뭉게구름처럼 보인다.
// Visible(시야 안·미점령)/Conquered 셀의 밝기는 FogOfWarRenderer(지형 틴트)가 그대로 담당한다.
[RequireComponent(typeof(GridMap))]
public class FogCloudRenderer : MonoBehaviour
{
    [Tooltip("구름 타일을 그릴 타일맵 - 정렬 레이어 Fog, 지형 타일맵과 같은 Grid 아래 자식이어야 한다.")]
    [SerializeField]
    private Tilemap _cloudTilemap;

    [Tooltip("낮에 표시할 구름 타일(cloud_01 기반, 청크 크기로 확대).")]
    [SerializeField]
    private TileBase _dayCloudTile;

    [Tooltip("밤에 표시할 구름 타일(cloud_02 기반, 청크 크기로 확대).")]
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
        PaintAllChunksNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid PaintAllChunksNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        PaintAllChunks();
        _gridMap.OnCellChanged.AddListener(HandleCellChanged);
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
    }

    // 셀 하나가 바뀌어도(예: 점령) 그 셀이 속한 청크 한 장만 다시 칠하면 된다.
    private void HandleCellChanged(GridCell cell)
    {
        Chunk chunk = _gridMap.GetChunkAt(cell.Coord);
        if (chunk != null)
            PaintChunk(chunk);
    }

    // 낮/밤이 바뀌면 현재 Hidden인 청크 전체를 다른 구름 타일로 다시 칠한다.
    private void HandleCycleChanged(CycleManager.CycleState _) => PaintAllChunks();

    private void PaintAllChunks()
    {
        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            PaintChunk(chunk);
        }
    }

    private void PaintChunk(Chunk chunk)
    {
        Vector3Int anchor = _gridMap.GetChunkAnchorCell(chunk.ChunkCoord);
        bool isHidden = chunk.CurrentState == ChunkState.Hidden;
        _cloudTilemap.SetTile(anchor, isHidden ? CurrentCloudTile : null);
    }

    private TileBase CurrentCloudTile =>
        _cycleManager != null && _cycleManager.CurrentCycle == CycleManager.CycleState.Night
            ? _nightCloudTile
            : _dayCloudTile;
}
