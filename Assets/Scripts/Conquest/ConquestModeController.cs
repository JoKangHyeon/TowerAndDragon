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
    private readonly List<Vector2Int> _conquerableChunkBuffer = new();
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

        Vector2Int? hoveredChunkCoord = chunk != null && chunk.CurrentState == ChunkState.Visible && chunk.DominantTerrain != TerrainType.Default
            ? chunk.ChunkCoord
            : (Vector2Int?)null;

        if (hoveredChunkCoord == _selectedChunkCoord)
            return;

        _selectedChunkCoord = hoveredChunkCoord;
        HighlightAllConquerableChunks();
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
            HighlightAllConquerableChunks();
        }
        else
        {
            if (_isSelectionLocked)
                _conquestUI.Close();

            _isSelectionLocked = false;
            _mouseSelectController.ClearHighlights();

            if (_chunkInfoRenderer != null)
                _chunkInfoRenderer.Clear();
        }
    }

    // 점령을 완료한 직후처럼 청크 상태가 바뀐 뒤 하이라이트를 다시 계산할 때 호출.
    public void RefreshConquerableHighlights()
    {
        if (IsActive)
            HighlightAllConquerableChunks();
    }

    // 패널이 열릴 때 호출 — 선택 셀을 고정하고 hover 갱신을 중단한다.
    public void LockChunkSelection(Vector2Int chunkCoord)
    {
        _isSelectionLocked = true;
        _selectedChunkCoord = chunkCoord;
        HighlightAllConquerableChunks();
    }

    // 패널이 닫힐 때 호출 — 잠금을 해제해 hover가 다시 하이라이트를 갱신하도록 한다.
    public void UnlockChunkSelection()
    {
        _isSelectionLocked = false;
    }

    // 보이는(Visible) 청크 전부를 점령 가능/불가능 색으로 한 번에 표시한다 - 호버해야만 알 수 있던 것을
    // 점령 모드 진입 즉시 전부 보여준다. 선택된 청크는 건물 재배치 선택과 동일한 노란색으로 구분 표시한다.
    private void HighlightAllConquerableChunks()
    {
        _conquerableBuffer.Clear();
        _blockedBuffer.Clear();
        _selectedBuffer.Clear();
        _conquerableChunkBuffer.Clear();

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            if (chunk.CurrentState != ChunkState.Visible)
                continue;

            if (chunk.DominantTerrain == TerrainType.Default)
                continue;

            bool canConquer = _conquestManager.CanSendExpedition(chunk.ChunkCoord);
            if (canConquer)
                _conquerableChunkBuffer.Add(chunk.ChunkCoord);

            if (_selectedChunkCoord.HasValue && chunk.ChunkCoord == _selectedChunkCoord.Value)
            {
                AddChunkCellCoords(chunk, _selectedBuffer);
                continue;
            }

            List<Vector3Int> target = canConquer ? _conquerableBuffer : _blockedBuffer;
            AddChunkCellCoords(chunk, target);
        }

        _mouseSelectController.HighlightCellGroups(_highlightGroups);

        if (_chunkInfoRenderer != null)
            _chunkInfoRenderer.Refresh(_conquerableChunkBuffer);
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

        bool isSelectableChunk = chunk != null
            && chunk.CurrentState == ChunkState.Visible
            && chunk.DominantTerrain != TerrainType.Default;

        if (isSelectableChunk)
        {
            _conquestUI.OnChunkSelected(chunk.ChunkCoord);
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
        foreach (GridCell cell in chunk.Cells)
        {
            if (cell.TerrainType != TerrainType.Default)
                target.Add(cell.Coord);
        }
    }

    bool IExclusiveMode.IsOpen => IsActive;
    void IExclusiveMode.Open() => SetConquestModeActive(true);
    void IExclusiveMode.Close() => SetConquestModeActive(false);
}
