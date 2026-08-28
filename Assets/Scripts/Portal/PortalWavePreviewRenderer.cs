using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;

/// <summary>
/// 낮 동안 각 포탈이 오늘 밤 내보낼 적 구성(종류 + 마릿수)을 미리 보여주는 마커를 배치한다.
///
/// 마커는 그 포탈의 직선 루트 스플라인 위에서 "안개(Hidden)로 덮이지 않은, 포탈에 가장 가까운 지점"에 뜬다.
/// 시야를 포탈 쪽으로 밀어낼수록 마커도 포탈 쪽으로 다가오므로, 정찰한 만큼 적 정보를 얻는 흐름이 위치로 드러난다.
///
/// 편성 해석은 WaveRoutePlanner에 맡긴다 - WaveManager(실제 스폰), PortalPathVisibility(경로 라인)와
/// 같은 결과를 보게 되어 표시된 마릿수가 실제 스폰 수와 어긋나지 않는다.
/// 배치는 ChunkInfoOverlayRenderer와 동일한 패턴(ComponentPool + 전체 재배치)을 따르고,
/// 카드는 값 주입만 받는 뷰(UI_PortalWavePreviewCard)다.
/// </summary>
public class PortalWavePreviewRenderer : MonoBehaviour
{
    // 직선 루트가 없는 포탈은 남은 첫 루트라도 기준으로 삼는다 - 마커를 아예 못 띄우는 것보다 낫다.
    private const int FALLBACK_ROUTE_INDEX = 0;

    [SerializeField] private WaveCycleProgression _waveCycleProgression;

    [Tooltip("점령 페널티로 늘어난 적 수까지 반영하기 위해 씁니다. WaveManager와 같은 참조여야 합니다.")]
    [SerializeField] private EnemyEnhancementManager _enemyEnhancementManager;

    [SerializeField] private List<Portal> _portals;

    [Tooltip("경로 위 지점이 안개에 덮였는지 판정하고, 지형 고저차를 얻는 데 씁니다.")]
    [SerializeField] private GridMap _gridMap;

    [Tooltip("밤에는 마커를 숨기기 위해 씁니다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("포탈 위에 띄울 예고 카드 프리팹(월드 스페이스).")]
    [SerializeField] private UI_PortalWavePreviewCard _cardPrefab;

    [Tooltip("아이콘 칸에 마우스를 올렸을 때 적 설명을 그릴 표시기(Ingame_window의 Tooltip_Layer). " +
             "비워두면 예고 카드는 그대로 뜨고 툴팁만 뜨지 않습니다.")]
    [SerializeField] private UI_TooltipPresenter _tooltipPresenter;

    [Tooltip("마커를 띄울 기준 경로(중앙 직선 통로)의 루트 인덱스.")]
    [SerializeField] private int _corridorRouteIndex = 1;

    [Tooltip("기준 경로를 훑는 간격(월드 단위). 작을수록 안개 경계에 정밀하게 붙습니다.")]
    [SerializeField] private float _corridorSampleStep = 0.5f;

    [Tooltip("경로 지면 기준 카드 미세 조정 오프셋.")]
    [SerializeField] private Vector3 _cardOffset = Vector3.zero;

    [Tooltip("예고 카드에 실제로 뜬 적 종류. 적 도감 해금(MonsterCodexDiscoveryController)이 이것을 듣는다. " +
             "안개에 덮여 카드가 뜨지 않은 적은 여기서 발화하지 않는다 - 정찰한 만큼만 해금되게 하기 위함이다.")]
    [SerializeField] private UnityEvent<MonsterData> _monsterPreviewShown = new();

    public UnityEvent<MonsterData> MonsterPreviewShown => _monsterPreviewShown;

    // 칸 순서를 루트 인덱스로 고정해, 편성 작성 순서가 바뀌어도 날마다 아이콘 순서가 뒤바뀌지 않게 한다.
    private static readonly Comparison<RouteSpawnPlan> ROUTE_ORDER =
        (left, right) => left.RouteIndex.CompareTo(right.RouteIndex);

    private readonly Dictionary<PortalDirection, Portal> _portalById = new();
    private readonly Dictionary<BaseMonster, MonsterIcon> _iconByMonsterPrefab = new();
    private readonly Dictionary<MonsterData, int> _entryIndexByMonster = new();
    private readonly List<RouteSpawnPlan> _sortedRoutes = new();
    private readonly List<(MonsterIcon Icon, int Count, MonsterData Data, EnemyEnhancementSnapshot Enhancement)> _entryBuffer = new();

    private ComponentPool<UI_PortalWavePreviewCard> _cardPool;
    private bool _isRefreshQueued;

    private void Awake()
    {
        _cardPool = new ComponentPool<UI_PortalWavePreviewCard>(_cardPrefab, transform);
        CachePortalMap();
    }

    private void OnEnable()
    {
        if (_waveCycleProgression == null || _gridMap == null)
        {
            Debug.LogError("[PortalWavePreviewRenderer] WaveCycleProgression 또는 GridMap 참조가 없습니다.", this);
            return;
        }

        // 표시기가 없어도 예고 카드 자체는 그대로 뜬다(툴팁만 안 뜬다). 다만 배선을 빠뜨린 것과
        // 일부러 비워 둔 것을 구분할 수 없으므로 경고는 남긴다.
        WiringGuard.Optional(_tooltipPresenter, nameof(_tooltipPresenter), this);

        _waveCycleProgression.DayWaveResolved.AddListener(HandleDayWaveResolved);

        // 안개 경계가 움직이면 마커도 따라가야 한다. OnChunkStateChanged는 점령 집합이 바뀔 때만 발화해서
        // 시야 확장(Hidden -> Visible)을 놓치므로, 셀 단위 이벤트를 받고 프레임당 한 번으로 묶는다.
        _gridMap.OnCellChanged.AddListener(HandleCellChanged);

        if (_enemyEnhancementManager != null)
        {
            _enemyEnhancementManager.ProfilesChanged += HandleProfilesChanged;
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(HandleCycleChanged);
        }

        // 툴팁 문구와 마릿수 라벨은 Setup 시점에 만들어 카드가 들고 있으므로, 언어가 바뀌면 다시 그려야
        // 이전 언어로 만들어 둔 문자열이 남지 않는다(UI_IngameWindow.RefreshLocalizedTexts와 같은 이유).
        StringTable.OnLanguageChanged += QueueRefresh;

        // 구독 직후 현재 상태를 한 번 반영한다. 다만 초기 시야(Castle.SetUpInitialTerritory)는 Start에서
        // 잡히므로, 다른 안개 렌더러들과 같은 이유로 한 프레임 뒤에 그린다.
        QueueRefresh();
    }

    private void OnDisable()
    {
        if (_waveCycleProgression != null)
        {
            _waveCycleProgression.DayWaveResolved.RemoveListener(HandleDayWaveResolved);
        }

        if (_gridMap != null)
        {
            _gridMap.OnCellChanged.RemoveListener(HandleCellChanged);
        }

        if (_enemyEnhancementManager != null)
        {
            _enemyEnhancementManager.ProfilesChanged -= HandleProfilesChanged;
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(HandleCycleChanged);
        }

        StringTable.OnLanguageChanged -= QueueRefresh;
    }

    private void CachePortalMap()
    {
        _portalById.Clear();

        if (_portals == null)
        {
            Debug.LogError("[PortalWavePreviewRenderer] 포탈 목록이 없습니다.", this);
            return;
        }

        foreach (Portal portal in _portals)
        {
            if (portal == null)
            {
                Debug.LogError("[PortalWavePreviewRenderer] 비어 있는 포탈 참조가 있습니다.", this);
                continue;
            }

            _portalById[portal.PortalDirectionId] = portal;
        }
    }

    private void HandleDayWaveResolved(int totalDay) => QueueRefresh();

    private void HandleProfilesChanged(TerrainType terrainType) => QueueRefresh();

    private void HandleCycleChanged(CycleManager.CycleState cycleState) => QueueRefresh();

    private void HandleCellChanged(GridCell cell) => QueueRefresh();

    // 청크 하나가 열리면 셀 이벤트가 청크 크기만큼(81회) 연달아 오므로, 실제 재배치는 프레임당 한 번만 한다.
    private void QueueRefresh()
    {
        if (_isRefreshQueued)
        {
            return;
        }

        _isRefreshQueued = true;
        RefreshNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RefreshNextFrameAsync(CancellationToken cancellationToken)
    {
        await UniTask.Yield(cancellationToken);

        _isRefreshQueued = false;
        Refresh();
    }

    private void Refresh()
    {
        if (!IsPreviewVisible)
        {
            _cardPool.DeactivateAll();
            return;
        }

        WaveDefinitionSO waveDefinition = _waveCycleProgression.CurrentSnapshot.WaveDefinition;

        if (waveDefinition == null)
        {
            Debug.LogError("[PortalWavePreviewRenderer] 오늘 웨이브 데이터가 없습니다.", this);
            _cardPool.DeactivateAll();
            return;
        }

        int usedCardCount = 0;

        foreach (PortalWaveData portalWave in waveDefinition.PortalWaves)
        {
            if (portalWave == null)
            {
                continue;
            }

            if (!_portalById.TryGetValue(portalWave.PortalDirectionId, out Portal portal))
            {
                Debug.LogError(
                    $"[PortalWavePreviewRenderer] {portalWave.PortalDirectionId} 방향 포탈을 찾을 수 없습니다.",
                    this);

                continue;
            }

            if (!portal.IsActive)
            {
                continue;
            }

            PortalRoutePlan portalPlan = WaveRoutePlanner.BuildPortalPlan(
                portalWave,
                portal,
                _enemyEnhancementManager);

            if (!portalPlan.HasSpawns)
            {
                continue;
            }

            if (!TryResolveAnchor(portal, out Vector3 anchor))
            {
                continue;
            }

            BuildEntries(portalPlan);

            if (_entryBuffer.Count == 0)
            {
                continue;
            }

            UI_PortalWavePreviewCard card = _cardPool.Get(usedCardCount);
            card.transform.position = anchor + _cardOffset;
            card.Setup(_entryBuffer, _tooltipPresenter);

            // 안개 판정(TryResolveAnchor)을 통과해 카드가 실제로 뜬 뒤에만 알린다 -
            // 정찰하지 않은 포탈의 적까지 해금되면 "정찰한 만큼 안다"는 예고 카드의 전제가 깨진다.
            for (int i = 0; i < _entryBuffer.Count; i++)
            {
                _monsterPreviewShown.Invoke(_entryBuffer[i].Data);
            }

            usedCardCount++;
        }

        _cardPool.DeactivateFrom(usedCardCount);
    }

    // 예고는 낮 전용이다. CycleManager가 없는 씬(팀원 테스트 씬 등)에서는 항상 보인다.
    private bool IsPreviewVisible =>
        _waveCycleProgression.HasCurrentSnapshot &&
        (_cycleManager == null || _cycleManager.CurrentCycle == CycleManager.CycleState.Day);

    // 기준 경로를 포탈 쪽(t=0)에서 성 쪽(t=1)으로 훑어, 처음으로 안개가 걷힌 지점을 찾는다.
    // t=0이 포탈 쪽인 것은 GroundSplineMovement가 0에서 출발해 성으로 걸어가는 것과 같은 전제다.
    //
    // 스플라인 위의 점은 아직 고저차가 반영되기 전의 평면 좌표이므로 셀 조회는 평면 역변환을 쓴다.
    // PickCellAtWorldPoint를 쓰면 높이를 두 번 반영해 언덕에서 어긋난다
    // (GroundSplineMovement.ResolveHeightOffset과 같은 이유).
    private bool TryResolveAnchor(Portal portal, out Vector3 anchor)
    {
        anchor = Vector3.zero;

        if (!portal.TryGetGroundPath(_corridorRouteIndex, out SplineContainer corridor) &&
            !portal.TryGetGroundPath(FALLBACK_ROUTE_INDEX, out corridor))
        {
            Debug.LogError(
                $"[PortalWavePreviewRenderer] {portal.PortalDirectionId} 포탈에 마커 기준 경로가 없습니다.",
                portal);

            return false;
        }

        float corridorLength = corridor.CalculateLength();

        if (corridorLength <= 0f || _corridorSampleStep <= 0f)
        {
            Debug.LogError(
                $"[PortalWavePreviewRenderer] {portal.PortalDirectionId} 포탈의 기준 경로 길이 또는 샘플 간격이 0 이하입니다.",
                portal);

            return false;
        }

        int sampleCount = Mathf.CeilToInt(corridorLength / _corridorSampleStep);

        for (int i = 0; i <= sampleCount; i++)
        {
            Vector3 flatPosition = corridor.EvaluatePosition((float)i / sampleCount);
            Vector3Int cellCoord = _gridMap.ConvertWorldToGrid(flatPosition);

            if (_gridMap.GetCellState(cellCoord) == ChunkState.Hidden)
            {
                continue;
            }

            flatPosition.y += _gridMap.GetHeightOffset(cellCoord);
            anchor = flatPosition;
            return true;
        }

        // 경로 전체가 안개에 덮여 있으면(성 주변까지 Hidden인 비정상 상태) 마커를 띄우지 않는다.
        return false;
    }

    // 같은 종류는 경로도 순서도 따지지 않고 한 칸으로 합친다 - 경로별로 나누면 같은 적이 여러 번
    // 나와 칸이 늘어나기만 하고 읽기 어려워진다. 한 경로 안에서 시차를 두고 쪼개진 무리
    // (SpawnGroupData.DelayBeforeGroup)도 마찬가지로 합쳐진다.
    // 루트 인덱스 순으로 훑어, 편성 작성 순서가 바뀌어도 아이콘 순서가 날마다 뒤바뀌지 않게 한다.
    // 버퍼는 카드가 Setup에서 즉시 소비하므로 포탈마다 재사용해도 안전하다.
    private void BuildEntries(PortalRoutePlan portalPlan)
    {
        _entryBuffer.Clear();
        _entryIndexByMonster.Clear();

        _sortedRoutes.Clear();
        _sortedRoutes.AddRange(portalPlan.Routes);
        _sortedRoutes.Sort(ROUTE_ORDER);

        foreach (RouteSpawnPlan routePlan in _sortedRoutes)
        {
            foreach (RuntimeSpawnGroup spawnGroup in routePlan.SpawnGroups)
            {
                MonsterData monsterData = spawnGroup.Source.MonsterData;

                if (monsterData == null)
                {
                    continue;
                }

                if (_entryIndexByMonster.TryGetValue(monsterData, out int existingIndex))
                {
                    // 강화는 첫 무리 것을 유지한다 - 같은 적은 어느 경로로 오든 같은 강화를 받으므로
                    // (EnemyEnhancementResolver가 몬스터 종류로만 규칙을 고른다) 덮어써도 같은 값이다.
                    (MonsterIcon icon, int count, MonsterData data, EnemyEnhancementSnapshot enhancement) =
                        _entryBuffer[existingIndex];

                    _entryBuffer[existingIndex] =
                        (icon, count + spawnGroup.SpawnCount, data, enhancement);

                    continue;
                }

                _entryIndexByMonster[monsterData] = _entryBuffer.Count;

                _entryBuffer.Add((
                    ResolveMonsterIcon(spawnGroup.Source.MonsterPrefab),
                    spawnGroup.SpawnCount,
                    monsterData,
                    spawnGroup.Enhancement));
            }
        }
    }

    // MonsterData에는 아이콘 필드가 없어, 인게임 프리팹의 스프라이트+색을 그대로 UI 아이콘으로 쓴다
    // (MonsterIcon 클래스 주석 참고). 프레임마다 다시 찾지 않도록 프리팹 단위로 캐시한다.
    private MonsterIcon ResolveMonsterIcon(BaseMonster monsterPrefab)
    {
        if (monsterPrefab == null)
        {
            return new MonsterIcon(null, Color.white);
        }

        if (_iconByMonsterPrefab.TryGetValue(monsterPrefab, out MonsterIcon cachedIcon))
        {
            return cachedIcon;
        }

        MonsterIcon icon = MonsterIcon.Resolve(monsterPrefab);
        _iconByMonsterPrefab[monsterPrefab] = icon;
        return icon;
    }
}
