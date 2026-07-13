using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
// --- 청크 디버깅용 (에디터 전용, 빌드 미포함) ---
// GridMap과 같은 오브젝트에 이 컴포넌트를 붙이면 디버깅 기능이 켜지고, 떼면 꺼진다.
// 1. 그룹핑/상태 육안 확인: _showChunkGizmos 켜고 Play 모드 진입
//    - Scene 뷰: 청크별 색이 다른 스프라이트로 그룹핑 확인, 초록/빨강/회색 구슬로 셀 상태(Active/Inactive/Unknown) 확인
//    - 청크 라벨에 "청크좌표 (셀개수) - 상태" 표시
// 2. 셀/청크 정보 확인: Play 모드에서 타일 좌클릭
//    - 인스펙터의 _debugSelectedCellCoord/State, _debugSelectedChunkCoord/State에 반영, 콘솔에도 로그
// 3. 청크->셀 상태 전파 테스트: 위 2번으로 셀을 먼저 선택한 뒤,
//    ChunkDebugger 컴포넌트 우클릭 > "디버그 - 선택 청크 상태 사이클 (청크 -> 셀)" 실행
//    - 선택된 청크 상태가 Unknown -> Inactive -> Active 순으로 바뀌며 소속 셀 전체 반영 여부를 PASS/FAIL로 로그
[RequireComponent(typeof(GridMap))]
public class ChunkDebugger : MonoBehaviour
{
    [SerializeField]
    private bool _showChunkGizmos = true;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private SpriteRenderer _chunkOverlaySpritePrefab;

    private GridMap _gridMap;
    private readonly List<SpriteRenderer> _chunkOverlayPool = new();

    private const int CHUNK_GIZMO_HASH_PRIME_X = 92821;
    private const int CHUNK_GIZMO_HASH_PRIME_Y = 68917;
    private const int CHUNK_GIZMO_HUE_STEPS = 360;
    private const float CHUNK_GIZMO_SATURATION = 1f;
    private const float CHUNK_GIZMO_VALUE = 1f;
    private const float CHUNK_GIZMO_ALPHA = 0.75f;
    private const float CHUNK_GIZMO_STATE_RADIUS = 0.15f;

    [Header("디버그 - 마우스로 선택한 셀/청크 정보")]
    [SerializeField]
    private Vector3Int _debugSelectedCellCoord;

    [SerializeField]
    private State _debugSelectedCellState;

    [SerializeField]
    private Vector2Int _debugSelectedChunkCoord;

    [SerializeField]
    private State _debugSelectedChunkState;

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
    }

    // GridMap.Awake()가 청크 생성을 끝낸 뒤(모든 Awake가 Start보다 먼저 실행됨)에 오버레이를 초기화
    private void Start()
    {
        RefreshChunkOverlay();
    }

    private void Update()
    {
        if (!Application.isPlaying || _mouseSelectController == null)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector3Int coord = _mouseSelectController.GetHoveredCell();
        Chunk chunk = _gridMap.GetChunkAt(coord);
        if (chunk == null)
            return;

        _debugSelectedCellCoord = coord;
        _debugSelectedCellState = _gridMap.GetCellState(coord);
        _debugSelectedChunkCoord = chunk.ChunkCoord;
        _debugSelectedChunkState = chunk.CurrentState;

        Debug.Log($"[ChunkDebugger] 선택 셀 {coord} - 셀 상태: {_debugSelectedCellState}, " +
            $"소속 청크: {_debugSelectedChunkCoord} - 청크 상태: {_debugSelectedChunkState}");
    }

    // 개발용 디버그 기즈모 - 청크별 소속 셀 상태를 색깔 있는 구슬로 표시 (그룹핑 자체는 RefreshChunkOverlay 스프라이트로 확인)
    private void OnDrawGizmos()
    {
        GridMap gridMap = _gridMap != null ? _gridMap : GetComponent<GridMap>();
        if (!_showChunkGizmos || gridMap == null)
            return;

        float yOffset = GetOverlayYOffset();

        foreach (Chunk chunk in gridMap.GetAllChunks())
        {
            foreach (GridCell cell in chunk.Cells)
            {
                Vector3 worldPos = gridMap.ConvertGridToWorld(cell.Coord);
                worldPos.y += yOffset;

                Gizmos.color = GetStateGizmoColor(cell.CurrentState);
                Gizmos.DrawWireSphere(worldPos, CHUNK_GIZMO_STATE_RADIUS);
            }

            if (chunk.Cells.Count > 0)
            {
                Vector3 labelPos = gridMap.ConvertGridToWorld(chunk.Cells[0].Coord);
                labelPos.y += yOffset;
                Handles.Label(labelPos, $"{chunk.ChunkCoord} ({chunk.Cells.Count}) - {chunk.CurrentState}");
            }
        }
    }

    private float GetOverlayYOffset() => _mouseSelectController != null ? _mouseSelectController.YOffset : 0f;

    // 마우스 셀렉트와 동일한 스프라이트 풀링 방식으로, 실제 렌더링되는 타일 위치(Y 오프셋 반영)에 맞춰
    // 청크별로 다른 색 스프라이트를 깔아 그룹핑을 확인
    private void RefreshChunkOverlay()
    {
        if (_chunkOverlaySpritePrefab == null || _gridMap == null)
            return;

        float yOffset = GetOverlayYOffset();
        int index = 0;

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            Color color = GetChunkGizmoColor(chunk.ChunkCoord);

            foreach (GridCell cell in chunk.Cells)
            {
                SpriteRenderer overlay = GetPooledChunkOverlay(index);
                Vector3 worldPos = _gridMap.ConvertGridToWorld(cell.Coord);
                worldPos.y += yOffset;
                overlay.transform.position = worldPos;
                overlay.color = color;
                index++;
            }
        }

        for (int i = index; i < _chunkOverlayPool.Count; i++)
        {
            _chunkOverlayPool[i].gameObject.SetActive(false);
        }
    }

    private SpriteRenderer GetPooledChunkOverlay(int index)
    {
        if (index >= _chunkOverlayPool.Count)
        {
            _chunkOverlayPool.Add(Instantiate(_chunkOverlaySpritePrefab, transform));
        }

        SpriteRenderer overlay = _chunkOverlayPool[index];
        overlay.gameObject.SetActive(_showChunkGizmos);
        return overlay;
    }

    private static Color GetChunkGizmoColor(Vector2Int chunkCoord)
    {
        int hash = chunkCoord.x * CHUNK_GIZMO_HASH_PRIME_X + chunkCoord.y * CHUNK_GIZMO_HASH_PRIME_Y;
        float hue = Mathf.Abs(hash % CHUNK_GIZMO_HUE_STEPS) / (float)CHUNK_GIZMO_HUE_STEPS;
        Color color = Color.HSVToRGB(hue, CHUNK_GIZMO_SATURATION, CHUNK_GIZMO_VALUE);
        color.a = CHUNK_GIZMO_ALPHA;
        return color;
    }

    // Active=초록, Inactive=빨강, Unknown=회색 작은 구슬로 셀 상태 표시 (청크 상태 변경 시 한번에 바뀌는지 확인용)
    private static Color GetStateGizmoColor(State state) => state switch
    {
        State.Active => Color.green,
        State.Inactive => Color.red,
        State.Unknown => Color.gray,
        _ => Color.white,
    };

    // 테스트용 - 마우스로 셀을 먼저 선택한 뒤 실행. 선택된 셀이 속한 청크의 상태를
    // Unknown -> Inactive -> Active 순으로 사이클하며, 소속 셀 전체에 한번에 반영되는지(청크->셀) 확인
    [ContextMenu("디버그 - 선택 청크 상태 사이클 (청크 -> 셀)")]
    private void DebugCycleSelectedChunkState()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[ChunkDebugger] 테스트 실패 - Play 모드에서만 실행 가능");
            return;
        }

        Chunk chunk = _gridMap.GetChunkAt(_debugSelectedCellCoord);
        if (chunk == null)
        {
            Debug.Log($"[ChunkDebugger] 테스트 실패 - {_debugSelectedCellCoord} 위치에 청크 없음 (먼저 마우스로 타일을 클릭하세요)");
            return;
        }

        State prevState = chunk.CurrentState;
        State nextState = GetNextDebugState(prevState);
        _gridMap.SetChunkState(_debugSelectedCellCoord, nextState);

        bool allCellsMatch = chunk.Cells.All(cell => cell.CurrentState == nextState);

        _debugSelectedCellState = chunk.Cells.First(cell => cell.Coord == _debugSelectedCellCoord).CurrentState;
        _debugSelectedChunkState = chunk.CurrentState;

        Debug.Log($"[ChunkDebugger] 청크 {chunk.ChunkCoord} 상태 {prevState} -> {nextState}, " +
            $"소속 셀 {chunk.Cells.Count}개 전체 반영: {(allCellsMatch ? "PASS" : "FAIL")}");
    }

    private static State GetNextDebugState(State current) => current switch
    {
        State.Unknown => State.Inactive,
        State.Inactive => State.Active,
        State.Active => State.Unknown,
        _ => State.Unknown,
    };
}
#endif
