using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
// --- 점령 시스템 디버깅용 (에디터 전용, 빌드 미포함, 추후 삭제 예정) ---
// UI·자원/인구 매니저 연동 전, ConquestManager의 핵심 로직(비용 검사 -> 원정 발송 -> 정산)만
// 따로 검증하기 위한 스크립트. ConquestManager 프로덕션 코드는 건드리지 않고,
// 필요한 내부 상태(_chunkCostTable, _durationTable, _activeExpeditions, CompleteConquest)는
// 리플렉션으로 들여다본다.
//
// 사용법 (Play 모드):
// 1. 마우스로 셀 좌클릭 -> 그 셀이 속한 청크가 포커싱됨 (콘솔 로그로 확인)
// 2. 키보드 1 -> 포커싱된 청크를 원정 대상으로 선택
// 3. 키보드 2 -> 선택된 대상의 필요 자원만큼 디버그 보유 자원을 충족시킴
// 4. 키보드 3 -> 원정 발송 시도. 성공하면 소요일수를 기다리지 않고 즉시 점령 완료 처리.
//    2번을 건너뛰고 바로 3번을 누르면 보유 자원이 0이라 자원 부족으로 실패한다.
[RequireComponent(typeof(GridMap))]
public class ConquestDebugger : MonoBehaviour
{
    [SerializeField]
    private ConquestManager _conquestManager;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    private GridMap _gridMap;

    [Header("디버그 - 현재 상태")]
    [SerializeField]
    private Vector2Int _focusedChunkCoord;

    [SerializeField]
    private bool _hasFocusedChunk;

    [SerializeField]
    private Vector2Int _selectedTargetChunkCoord;

    [SerializeField]
    private bool _hasSelectedTarget;

    [SerializeField]
    private ResourceCost _debugAvailableResources;

    private static readonly FieldInfo ChunkCostTableField =
        typeof(ConquestManager).GetField("_chunkCostTable", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo DurationTableField =
        typeof(ConquestManager).GetField("_durationTable", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly FieldInfo ActiveExpeditionsField =
        typeof(ConquestManager).GetField("_activeExpeditions", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo CompleteConquestMethod =
        typeof(ConquestManager).GetMethod("CompleteConquest", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo FindConstructableCellMethod =
        typeof(ConquestManager).GetMethod("FindConstructableCellNearestCenter", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo HasConqueredOrthogonalNeighborMethod =
        typeof(ConquestManager).GetMethod("HasConqueredOrthogonalNeighbor", BindingFlags.NonPublic | BindingFlags.Instance);

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        HandleMouseFocus();
        HandleKeyboardInput();
    }

    private void HandleMouseFocus()
    {
        if (_mouseSelectController == null || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector3Int hoveredCell = _mouseSelectController.GetHoveredCell();
        Chunk chunk = _gridMap.GetChunkAt(hoveredCell);
        if (chunk == null)
            return;

        _focusedChunkCoord = chunk.ChunkCoord;
        _hasFocusedChunk = true;

        bool hasOrthogonalConqueredNeighbor = HasConqueredOrthogonalNeighbor(chunk.ChunkCoord);
        Debug.Log($"[ConquestDebugger] 포커스 청크: {_focusedChunkCoord} ({chunk.DominantTerrain}) - 상태: {chunk.CurrentState}, " +
            $"4방향 인접 Conquered: {(hasOrthogonalConqueredNeighbor ? "있음" : "없음")}");
    }

    // ConquestManager의 실제 판단 로직을 그대로 리플렉션으로 재사용 - 규칙을 따로 베끼지 않아
    // 나중에 인접 규칙이 바뀌어도 디버그 로그가 낡은 기준으로 어긋날 일이 없다.
    private bool HasConqueredOrthogonalNeighbor(Vector2Int chunkCoord) =>
        (bool)HasConqueredOrthogonalNeighborMethod.Invoke(_conquestManager, new object[] { chunkCoord });

    private void HandleKeyboardInput()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SelectFocusedChunkAsTarget();

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            FulfillDebugResources();

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            TrySendAndForceComplete();
    }

    private void SelectFocusedChunkAsTarget()
    {
        if (!_hasFocusedChunk)
        {
            Debug.Log("[ConquestDebugger] 먼저 마우스로 청크를 선택하세요 (셀 좌클릭)");
            return;
        }

        _selectedTargetChunkCoord = _focusedChunkCoord;
        _hasSelectedTarget = true;
        _debugAvailableResources = default;

        Debug.Log($"[ConquestDebugger] 원정 대상 선택: {_selectedTargetChunkCoord}");
    }

    private void FulfillDebugResources()
    {
        if (!_hasSelectedTarget)
        {
            Debug.Log("[ConquestDebugger] 1번으로 먼저 원정 대상을 선택하세요");
            return;
        }

        if (!TryGetChunkCost(_selectedTargetChunkCoord, out ResourceCost cost))
        {
            Debug.LogWarning($"[ConquestDebugger] 청크 {_selectedTargetChunkCoord}의 비용 데이터가 없습니다");
            return;
        }

        _debugAvailableResources = cost;
        Debug.Log($"[ConquestDebugger] 자원 충족 처리 - {FormatResourceCost(cost)}");
    }

    private void TrySendAndForceComplete()
    {
        if (!_hasSelectedTarget)
        {
            Debug.Log("[ConquestDebugger] 1번으로 먼저 원정 대상을 선택하세요");
            return;
        }

        Vector2Int targetChunkCoord = _selectedTargetChunkCoord;
        Chunk chunk = _gridMap.GetChunk(targetChunkCoord);

        TryGetChunkCost(targetChunkCoord, out ResourceCost cost);
        TryGetDaysRequired(targetChunkCoord, out int daysRequired, out TerrainType terrainType);

        bool hasOrthogonalConqueredNeighbor = HasConqueredOrthogonalNeighbor(targetChunkCoord);
        Debug.Log($"[ConquestDebugger] 청크 {targetChunkCoord} ({terrainType}) - 필요 자원: {FormatResourceCost(cost)}, 소요일수: {daysRequired}일, " +
            $"4방향 인접 Conquered: {(hasOrthogonalConqueredNeighbor ? "있음" : "없음")}");

        // 점령 완료(주둔지 배치) 전에 미리 예상 위치를 구해둔다 - 완료 후에는 이미 점유돼 있어 같은 방식으로 못 구함.
        GridCell expectedGarrisonCell = chunk != null
            ? (GridCell)FindConstructableCellMethod.Invoke(_conquestManager, new object[] { chunk })
            : null;

        bool sent = _conquestManager.SendExpedition(targetChunkCoord, _debugAvailableResources);
        if (!sent)
        {
            string reasonHint = !hasOrthogonalConqueredNeighbor
                ? "4방향 인접 Conquered 청크 없음 (대각선 인접만으로는 점령 불가)"
                : "자원 부족 또는 이미 진행 중인 원정 존재";
            Debug.Log($"[ConquestDebugger] 점령 실패 - {targetChunkCoord} ({reasonHint})");
            return;
        }

        bool forced = ForceCompleteExpedition(targetChunkCoord);
        Debug.Log($"[ConquestDebugger] 점령 성공(즉시 완료 {(forced ? "PASS" : "FAIL")}) - {targetChunkCoord}, 청크 상태: {chunk?.CurrentState}");

        LogGarrisonPlacement(targetChunkCoord, expectedGarrisonCell);
    }

    // ConquestManager.PlaceGarrison()이 실제로 청크 중앙에 가장 가까운 건설 가능 셀에 주둔지를 세웠는지 검증
    private void LogGarrisonPlacement(Vector2Int chunkCoord, GridCell expectedGarrisonCell)
    {
        if (expectedGarrisonCell == null)
        {
            Debug.LogWarning($"[ConquestDebugger] 주둔지 배치 검증 불가 - 청크 {chunkCoord}에 건설 가능한 셀이 없었음");
            return;
        }

        bool isOccupied = expectedGarrisonCell.ExistTypeOnCell == ExistTypeOnCell.Building;
        string buildingName = expectedGarrisonCell.OccupantBuilding != null ? expectedGarrisonCell.OccupantBuilding.name : "없음";

        Debug.Log($"[ConquestDebugger] 주둔지 배치 검증 {(isOccupied ? "PASS" : "FAIL")} - 예상 위치(청크 중앙 최근접) {expectedGarrisonCell.Coord}, 실제 점유: {buildingName}");
    }

    private bool TryGetChunkCost(Vector2Int chunkCoord, out ResourceCost cost)
    {
        var chunkCostTable = (ConquestChunkCostTable)ChunkCostTableField.GetValue(_conquestManager);
        if (chunkCostTable == null)
        {
            cost = default;
            return false;
        }

        return chunkCostTable.TryResolve(chunkCoord, out cost);
    }

    private bool TryGetDaysRequired(Vector2Int chunkCoord, out int daysRequired, out TerrainType terrainType)
    {
        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        if (chunk == null)
        {
            daysRequired = 0;
            terrainType = TerrainType.Default;
            return false;
        }

        terrainType = chunk.DominantTerrain;

        var durationTable = (ConquestDurationTable)DurationTableField.GetValue(_conquestManager);
        daysRequired = durationTable != null ? durationTable.ResolveDaysRequired(terrainType) : 0;
        return true;
    }

    private bool ForceCompleteExpedition(Vector2Int targetChunkCoord)
    {
        var activeExpeditions = (List<ConquestExpedition>)ActiveExpeditionsField.GetValue(_conquestManager);

        for (int i = activeExpeditions.Count - 1; i >= 0; i--)
        {
            ConquestExpedition expedition = activeExpeditions[i];
            if (expedition.TargetChunkCoord != targetChunkCoord)
                continue;

            CompleteConquestMethod.Invoke(_conquestManager, new object[] { expedition });
            activeExpeditions.RemoveAt(i);
            return true;
        }

        return false;
    }

    private static string FormatResourceCost(ResourceCost cost) =>
        $"인구{cost.Population} 식량{cost.Food} 목재{cost.Wood} 석재{cost.Stone} 광물{cost.Ore}";
}
#endif
