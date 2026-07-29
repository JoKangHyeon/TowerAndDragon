using System.Collections.Generic;
using UnityEngine;

// 봉인석 건설 진행을 추적하고, 포탈 4개 전부에 봉인석이 지어지는 순간 4포탈을 동시에 봉인 + 승리로 전환한다.
// 봉인석 하나를 짓는다고 그 포탈이 즉시 봉인되지는 않는다 - 개별 포탈은 전부 모일 때까지 미봉인 상태를 유지한다.
public sealed class PortalSealManager : MonoBehaviour, ISealStonePlacementQuery
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private PortalSealTable _table;
    [SerializeField] private List<Portal> _portals;

    // 연구 시스템 연동 자리 - null이면 해금된 것으로 취급(GridMap의 다른 쿼리 프로퍼티와 동일한 관례).
    public ISealStoneUnlockQuery UnlockQuery { get; set; }

    private readonly Dictionary<Vector3Int, PortalDirection> _siteLookup = new();

    // "봉인석이 지어진 포탈" 집합 - 개별 포탈은 이 시점에 아직 봉인되지 않는다(Seal() 호출 안 함).
    // 사이트 전부(SiteCount)가 모이는 순간에만 한꺼번에 봉인 + 승리로 전환한다.
    private readonly HashSet<PortalDirection> _stonesBuiltAt = new();

    // ISealStonePlacementQuery.IsUnlocked 구현 - UnlockQuery가 비어있으면 해금된 것으로 취급.
    public bool IsUnlocked => UnlockQuery == null || UnlockQuery.IsSealStoneUnlocked;

    public int BuiltCount => _stonesBuiltAt.Count;

    private void Awake() => Construct(_gridMap, _gameManager, _table, _portals);

    // 씬 배선(Awake)과 테스트가 공유하는 배선 진입점 - Inspector로 채워진 참조를 Awake가 그대로
    // 넘겨 호출하거나, 테스트가 코드로 구성한 참조를 리플렉션 없이 직접 넘길 수 있다.
    // 여러 번 호출해도 안전(멱등) - _siteLookup을 매번 다시 전개하고 쿼리 등록도 다시 한다.
    public void Construct(GridMap gridMap, GameManager gameManager, PortalSealTable table, List<Portal> portals)
    {
        _gridMap = gridMap;
        _gameManager = gameManager;
        _table = table;
        _portals = portals;

        _siteLookup.Clear();
        if (_table != null)
            _table.BuildSiteLookup(_siteLookup);

        if (_gridMap != null)
            _gridMap.SealStonePlacementQuery = this;
    }

    private void OnEnable()
    {
        if (_gridMap != null)
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
    }

    private void OnDisable()
    {
        if (_gridMap != null)
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
    }

    private void OnDestroy()
    {
        if (_gridMap != null && ReferenceEquals(_gridMap.SealStonePlacementQuery, this))
            _gridMap.SealStonePlacementQuery = null;
    }

    private void HandleBuildingAdded(Building building)
    {
        if (_gridMap == null || !(building is SealStone))
            return;

        // 건설 이후 시점 - footprint가 이미 배치 게이트(TryResolveOpenSite)를 통과했으므로
        // "같은 포탈 안"인지는 다시 안 따지고 좌표로만 방향을 찾는다.
        if (TryResolveSingleSite(_gridMap.GetFootprintCoords(building), out PortalDirection direction))
            RegisterStoneBuilt(direction);
    }

    // "어느 포탈에 봉인석이 지어졌는가"가 확정된 뒤의 순수 로직 - GridMap 의존이 없어 직접 테스트 가능.
    // HandleBuildingAdded의 실제 구독 콜백에서도 이 메서드를 그대로 쓴다(테스트 전용 우회 경로가 아니다).
    public void RegisterStoneBuilt(PortalDirection direction)
    {
        if (_table == null)
            return;

        _stonesBuiltAt.Add(direction);

        if (_stonesBuiltAt.Count < _table.SiteCount)
            return; // 아직 전부 안 모임 - 개별 포탈은 여전히 미봉인 상태

        if (_portals != null)
        {
            foreach (Portal portal in _portals)
            {
                if (portal != null)
                    portal.Seal(); // Seal() 자체가 _isActive = false까지 처리 - 프레임 지연 없음
            }
        }

        if (_gameManager != null)
            _gameManager.Victory(); // TrySetGameResult가 idempotent라 보스 격파 경로와 동시에 들어와도 안전
    }

    // ISealStonePlacementQuery - GridMap이 배치 판정에서 호출.
    public bool CellIsSealSite(Vector3Int coord) => _siteLookup.ContainsKey(coord);

    public bool TryResolveOpenSite(List<Vector3Int> footprint, out PortalDirection direction) =>
        TryResolveSingleSite(footprint, out direction) && !_stonesBuiltAt.Contains(direction);

    // footprint 전체 셀이 "같은" 포탈 영역 안에 있을 때만 그 방향을 반환한다(하나라도 벗어나거나
    // 두 포탈에 걸치면 false) - "이미 봉인석이 있는가"는 여기서 보지 않는다(TryResolveOpenSite가 덧붙인다).
    private bool TryResolveSingleSite(IReadOnlyList<Vector3Int> footprint, out PortalDirection direction)
    {
        direction = PortalDirection.None;

        if (footprint == null || footprint.Count == 0)
            return false;

        if (!_siteLookup.TryGetValue(footprint[0], out direction))
            return false;

        for (int i = 1; i < footprint.Count; i++)
        {
            if (!_siteLookup.TryGetValue(footprint[i], out PortalDirection other) || other != direction)
                return false;
        }

        return true;
    }

    // PortalProgressionController.OnValidate와 동일한 목적의 방어 검사 - 이 매니저의 _portals와
    // 그 컨트롤러의 _portals는 씬에 손으로 각각 배선하는 두 리스트라 어긋날 수 있다.
    // null/중복 방향 검사에 더해, 테이블의 사이트 개수와 포탈 개수가 일치하는지도 확인한다 -
    // 안 맞으면(예: 테이블에 3개만 저작) SiteCount가 실제 포탈 수보다 작아져 3개만 지어도 승리가 터진다.
    private void OnValidate()
    {
        if (_portals == null)
            return;

        HashSet<PortalDirection> registeredPortalDirections = new HashSet<PortalDirection>();

        foreach (Portal portal in _portals)
        {
            if (portal == null)
            {
                Debug.LogError("[PortalSealManager] 비어 있는 포탈 참조가 있습니다.", this);
                continue;
            }

            if (!registeredPortalDirections.Add(portal.PortalDirectionId))
            {
                Debug.LogError(
                    $"[PortalSealManager] {portal.PortalDirectionId} 포탈이 중복되었습니다.",
                    this);
            }
        }

        if (_table != null && _table.SiteCount != _portals.Count)
        {
            Debug.LogError(
                $"[PortalSealManager] 봉인 영역 테이블의 사이트 개수({_table.SiteCount})와 포탈 개수({_portals.Count})가 다릅니다.",
                this);
        }
    }
}
