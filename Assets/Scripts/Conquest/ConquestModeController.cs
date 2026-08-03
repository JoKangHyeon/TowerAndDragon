using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// 점령 모드 진입/청크 선택을 담당한다. 건설 모드(BuildingPlacementController)와 동일한
// 입력 처리 패턴(포인터-오버-UI 가드)을 따른다.
// _selectAction("Confirm")은 BuildingPlacementController._placeAction과 같은 공유 액션이며
// GlobalInputBootstrap이 한 번만 Enable한다 - 이 컨트롤러는 스스로 Enable/Disable하지 않는다.
public class ConquestModeController : MonoBehaviour, IExclusiveMode
{
    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private ConquestManager _conquestManager;

    [SerializeField]
    private UI_ConquestWindow _conquestUI;

    [SerializeField]
    private BuildingPlacementController _buildingPlacementController;

    [SerializeField]
    private ChunkInfoOverlayRenderer _chunkInfoRenderer;

    [Tooltip("호버/선택한 청크를 점령하면 얻게 될 땅의 경계선을 미리 그린다. 씬에서 Grid 오브젝트를 연결한다.")]
    [SerializeField]
    private ConqueredChunkBorderRenderer _borderRenderer;

    [SerializeField]
    private InputActionReference _selectAction;

    [Tooltip("점령 모드를 닫는 액션 - 패널이 열려 있으면 패널만 닫고, 없으면 점령 모드 자체를 끈다.")]
    [SerializeField]
    private InputActionReference _closeAction;

    [SerializeField]
    private Color _conquerableHighlightColor = Color.green;

    [SerializeField]
    private Color _blockedHighlightColor = Color.red;

    [Tooltip("누른 뒤 이 픽셀 이상 포인터가 움직이면 클릭이 아니라 드래그로 간주해 선택/닫힘을 무시한다.")]
    [SerializeField]
    private float _dragThreshold = 10f;

    public bool IsActive { get; private set; }

    private Vector2Int? _selectedChunkCoord;
    private bool _isSelectionLocked;
    private Vector2 _pressScreenPosition;

    private readonly List<Vector3Int> _conquerableBuffer = new();
    private readonly List<Vector3Int> _blockedBuffer = new();
    private readonly List<Vector3Int> _selectedBuffer = new();

    // 영토가 바뀔 때만 재계산되는 청크 분류 캐시 - 호버마다 다시 계산하지 않는다.
    private readonly List<Vector2Int> _conquerableChunkBuffer = new();
    private readonly List<Vector2Int> _blockedChunkBuffer = new();

    // 점령 대상 청크별로 "이 청크를 점령하면 딸려올 짜투리 청크" - 분류와 같이 갱신되는 캐시.
    private readonly Dictionary<Vector2Int, List<Vector2Int>> _annexableNeighborsByChunk = new();

    // 위 캐시의 역방향 - 짜투리 청크를 호버/클릭했을 때 어느 점령 대상으로 이어줄지.
    private readonly Dictionary<Vector2Int, Vector2Int> _annexOwnerByChunk = new();
    private readonly List<Vector2Int> _annexPreviewChunkBuffer = new();

    // 경계선을 그릴 대상(호버/선택 청크 + 그 편입 청크)과, 이번 갱신에서 이미 칠한 청크.
    private readonly HashSet<Vector2Int> _previewChunkCoords = new();
    private readonly HashSet<Vector2Int> _paintedChunkCoords = new();

    private static readonly List<Vector2Int> EMPTY_CHUNK_COORDS = new();
    private (List<Vector3Int> Coords, Color Color)[] _highlightGroups;

    private void Awake()
    {
        _highlightGroups = new (List<Vector3Int> Coords, Color Color)[]
        {
            (_conquerableBuffer, _conquerableHighlightColor),
            (_blockedBuffer, _blockedHighlightColor),
            (_selectedBuffer, _mouseSelectController.SelectionHighlightColor)
        };
    }

    private void Update()
    {
        if (!IsActive)
            return;

        HandleHover();
        HandleSelectInput();
        HandleCloseInput();
    }

    // ESC 등 닫기 입력 처리 2단계 - 패널(청크 정보)이 열려 있으면 패널만 닫고,
    // 패널이 없는 상태(청크만 하이라이트된 상태)면 점령 모드 자체를 끈다.
    private void HandleCloseInput()
    {
        if (_closeAction == null || !_closeAction.action.WasPerformedThisFrame())
            return;

        if (_isSelectionLocked)
            _conquestUI.Close();
        else
            SetConquestModeActive(false);
    }

    // 매 프레임 호버된 청크를 확인해, 바뀐 경우에만 노란색 선택 표시를 다시 계산한다(클릭을 기다리지 않는다).
    private void HandleHover()
    {
        if (_isSelectionLocked)
            return;

        Vector3Int hoveredCell = _mouseSelectController.GetHoveredCell();
        Chunk chunk = _gridMap.GetChunkAt(hoveredCell);

        Vector2Int? hoveredChunkCoord = TryResolveConquestTarget(chunk, out Vector2Int targetChunkCoord)
            ? targetChunkCoord
            : (Vector2Int?)null;

        if (hoveredChunkCoord == _selectedChunkCoord)
            return;

        _selectedChunkCoord = hoveredChunkCoord;
        RebuildHighlightBuffers();
    }

    // 편입 예정 짜투리 청크도 점령 대상과 같은 색으로 칠해지므로 플레이어는 그 위도 똑같이 겨냥한다.
    // 하지만 짜투리 청크는 코스트 테이블에 없어 그 자체로는 호버/선택 대상이 아니므로,
    // 그 위에서는 자기를 편입시킬 점령 대상 청크를 대신 가리키게 한다.
    private bool TryResolveConquestTarget(Chunk chunk, out Vector2Int targetChunkCoord)
    {
        targetChunkCoord = default;

        if (chunk == null)
            return false;

        if (chunk.CurrentState == ChunkState.Visible && _conquestManager.HasExpeditionCost(chunk.ChunkCoord))
        {
            targetChunkCoord = chunk.ChunkCoord;
            return true;
        }

        return _annexOwnerByChunk.TryGetValue(chunk.ChunkCoord, out targetChunkCoord);
    }

    public void SetConquestModeActive(bool isActive)
    {
        IsActive = isActive;
        _selectedChunkCoord = null;

        // 점령 모드 동안에는 건물 배치 컨트롤러가 같은 클릭을 처리해 하이라이트를 지우지 못하도록 입력을 억제한다.
        _buildingPlacementController.InputSuppressed = isActive;

        if (isActive)
        {
            _buildingPlacementController.CancelAll();
            RecomputeConquerableClassification();
        }
        else
        {
            if (_isSelectionLocked)
                _conquestUI.Close();

            _isSelectionLocked = false;
            _mouseSelectController.ClearHighlights();

            if (_borderRenderer != null)
                _borderRenderer.ClearPreviewBorder();

            if (_chunkInfoRenderer != null)
                _chunkInfoRenderer.Clear();
        }
    }

    // 점령을 완료한 직후처럼 청크 상태가 바뀐 뒤 하이라이트를 다시 계산할 때 호출.
    public void RefreshConquerableHighlights()
    {
        if (IsActive)
            RecomputeConquerableClassification();
    }

    // 패널이 열릴 때 호출 — 선택 셀을 고정하고 hover 갱신을 중단한다.
    public void LockChunkSelection(Vector2Int chunkCoord)
    {
        _isSelectionLocked = true;
        _selectedChunkCoord = chunkCoord;
        RebuildHighlightBuffers();
    }

    // 패널이 닫힐 때 호출 — 잠금을 해제해 hover가 다시 하이라이트를 갱신하도록 한다.
    public void UnlockChunkSelection()
    {
        _isSelectionLocked = false;
    }

    // 코스트 테이블 조회 + 원정 가능 여부 판정으로 Visible 청크를 점령 가능/불가로 분류한다 - 무거운 부분.
    private void RecomputeConquerableClassification()
    {
        _conquerableChunkBuffer.Clear();
        _blockedChunkBuffer.Clear();
        _annexableNeighborsByChunk.Clear();
        _annexOwnerByChunk.Clear();

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            if (chunk.CurrentState != ChunkState.Visible)
                continue;

            if (!_conquestManager.HasExpeditionCost(chunk.ChunkCoord))
                continue;

            if (_conquestManager.CanSendExpedition(chunk.ChunkCoord))
                _conquerableChunkBuffer.Add(chunk.ChunkCoord);
            else
                _blockedChunkBuffer.Add(chunk.ChunkCoord);
        }

        CacheAnnexableNeighbors(_conquerableChunkBuffer);
        CacheAnnexableNeighbors(_blockedChunkBuffer);

        if (_chunkInfoRenderer != null)
            _chunkInfoRenderer.Refresh(_conquerableChunkBuffer);

        RebuildHighlightBuffers();
    }

    // 각 점령 대상 청크를 점령했을 때 함께 편입될 짜투리 청크를 미리 구해 둔다 - 판정 결과는
    // 영토 상태(이웃의 Conquered 여부)에만 의존하므로 분류와 같은 주기로만 갱신하면 된다.
    private void CacheAnnexableNeighbors(List<Vector2Int> chunkCoords)
    {
        foreach (Vector2Int coord in chunkCoords)
        {
            _conquestManager.CollectAnnexableNeighbors(coord, _annexPreviewChunkBuffer);

            var annexableNeighbors = new List<Vector2Int>();
            foreach (Vector2Int annexCoord in _annexPreviewChunkBuffer)
            {
                // 실제 편입은 ExpandVisibility가 먼저 돌아 이웃이 전부 Visible이 된 뒤에 일어나지만,
                // 여기는 점령 전이라 아직 Hidden인 청크가 섞일 수 있다 - 미탐색 지역을 미리 드러내지
                // 않도록 걸러낸다(편입 판정 자체를 바꾸면 안 되므로 표시 쪽에서만).
                Chunk annexChunk = _gridMap.GetChunk(annexCoord);
                if (annexChunk == null || annexChunk.CurrentState == ChunkState.Hidden)
                    continue;

                annexableNeighbors.Add(annexCoord);

                // 짜투리 청크 하나가 여러 점령 대상의 편입 후보일 수 있다 - 점령 가능(초록) 목록을
                // 먼저 캐싱하므로 TryAdd가 하이라이트 색 우선순위와 같은 주인을 남긴다.
                _annexOwnerByChunk.TryAdd(annexCoord, coord);
            }

            if (annexableNeighbors.Count > 0)
                _annexableNeighborsByChunk[coord] = annexableNeighbors;
        }
    }

    // 분류 결과를 셀 단위 색상 버퍼로 옮겨 그린다 - 호버마다 도는 가벼운 부분.
    private void RebuildHighlightBuffers()
    {
        _conquerableBuffer.Clear();
        _blockedBuffer.Clear();
        _selectedBuffer.Clear();
        _paintedChunkCoords.Clear();

        // 선택 청크를 먼저 칠해, 같은 짜투리 청크를 공유하는 다른 청크의 색에 덮이지 않게 한다.
        AddSelectedChunkCells();

        AddClassifiedChunkCells(_conquerableChunkBuffer, _conquerableBuffer);
        AddClassifiedChunkCells(_blockedChunkBuffer, _blockedBuffer);

        _mouseSelectController.HighlightCellGroups(_highlightGroups);
    }

    // 호버/선택한 청크와 그 편입 예정 청크를 선택색으로 칠하고,
    // 두 영역을 합친 "점령하면 얻게 될 땅"의 바깥 경계선을 그린다.
    private void AddSelectedChunkCells()
    {
        _previewChunkCoords.Clear();

        // 선택 상태가 잠긴 채로 점령이 완료되면 그 청크가 분류에서 빠지므로(이미 Conquered),
        // 여전히 점령 대상인지 확인한 뒤에만 칠한다.
        if (_selectedChunkCoord.HasValue && IsClassifiedChunk(_selectedChunkCoord.Value))
        {
            AddChunkCellsOnce(_selectedChunkCoord.Value, _selectedBuffer);
            _previewChunkCoords.Add(_selectedChunkCoord.Value);

            foreach (Vector2Int coord in GetAnnexableNeighbors(_selectedChunkCoord.Value))
            {
                AddChunkCellsOnce(coord, _selectedBuffer);
                _previewChunkCoords.Add(coord);
            }
        }

        if (_borderRenderer != null)
            _borderRenderer.ShowPreviewBorder(_previewChunkCoords);
    }

    private bool IsClassifiedChunk(Vector2Int chunkCoord) =>
        _conquerableChunkBuffer.Contains(chunkCoord) || _blockedChunkBuffer.Contains(chunkCoord);

    // 점령 대상 청크와 함께, 그 청크를 점령하면 딸려올 짜투리 청크까지 같은 색으로 칠한다.
    private void AddClassifiedChunkCells(List<Vector2Int> chunkCoords, List<Vector3Int> target)
    {
        foreach (Vector2Int coord in chunkCoords)
        {
            AddChunkCellsOnce(coord, target);

            foreach (Vector2Int annexCoord in GetAnnexableNeighbors(coord))
            {
                AddChunkCellsOnce(annexCoord, target);
            }
        }
    }

    private List<Vector2Int> GetAnnexableNeighbors(Vector2Int chunkCoord) =>
        _annexableNeighborsByChunk.TryGetValue(chunkCoord, out List<Vector2Int> annexableNeighbors)
            ? annexableNeighbors
            : EMPTY_CHUNK_COORDS;

    // 하나의 짜투리 청크가 여러 점령 대상의 편입 후보일 수 있으므로, 이미 칠한 청크는 건너뛴다 -
    // 같은 자리에 하이라이트 스프라이트가 겹쳐 쌓이는 것을 막는다.
    private void AddChunkCellsOnce(Vector2Int chunkCoord, List<Vector3Int> target)
    {
        if (!_paintedChunkCoords.Add(chunkCoord))
            return;

        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        if (chunk == null)
            return;

        AddChunkCellCoords(chunk, target);
    }

    private void HandleSelectInput()
    {
        if (_selectAction == null)
            return;

        // 누른 순간의 포인터 위치를 기록해 둔다.
        if (_selectAction.action.WasPressedThisFrame())
        {
            _pressScreenPosition = PointerScreenPosition();
        }

        // 판정은 뗄 때 한다. 누른 지점에서 임계값 이상 움직였으면 드래그로 보고 선택/닫힘을 무시한다.
        if (!_selectAction.action.WasReleasedThisFrame())
            return;

        if (Vector2.Distance(_pressScreenPosition, PointerScreenPosition()) > _dragThreshold)
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3Int hoveredCell = _mouseSelectController.GetHoveredCell();
        Chunk chunk = _gridMap.GetChunkAt(hoveredCell);

        // 짜투리 청크를 클릭하면 그 청크가 아니라 자기를 편입시킬 점령 대상 청크의 패널을 연다
        // (짜투리 청크 자체는 코스트 테이블에 없어 패널에 띄울 비용/보상 데이터가 없다).
        if (TryResolveConquestTarget(chunk, out Vector2Int targetChunkCoord))
        {
            _conquestUI.OnChunkSelected(targetChunkCoord);
            return;
        }

        // 선택 가능한 청크(그리드 표시가 뜬 곳)가 아닌 빈 공간을 클릭하면, 열려 있던 패널을 닫는다.
        if (_isSelectionLocked)
            _conquestUI.Close();
    }

    private static Vector2 PointerScreenPosition() =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    private static void AddChunkCellCoords(Chunk chunk, List<Vector3Int> target)
    {
        foreach (Vector3Int coord in chunk.LandCellCoords)
            target.Add(coord);
    }

    bool IExclusiveMode.IsOpen => IsActive;
    void IExclusiveMode.Open() => SetConquestModeActive(true);
    void IExclusiveMode.Close() => SetConquestModeActive(false);
}
