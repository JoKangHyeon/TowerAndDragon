using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 인구 배치 연출의 유일한 진입점. 성에서 캐릭터를 내보내고, 목적지를 계산하고, 상주 인원을 관리한다.
///
/// 순수 장식이다 - 인구 배치 로직은 이 시스템 없이도 완전히 동작하며, 인스펙터 연결을 전부 비워도
/// 경고만 남고 게임은 정상 동작한다.
///
/// 핵심 설계 두 가지:
///
/// 1. 상주 캐릭터(생산시설·연구소·랜드마크·원정 크루)는 이벤트가 아니라 "상태"에서 파생시킨다.
///    상주 = AssignedPopulation &gt; 0 의 함수, 크루 = ConquestManager.ActiveExpeditions 의 함수다.
///    그래서 세이브에 저장할 것이 하나도 없고, 로드하면 저절로 맞는 그림이 복원된다.
///    크루 인원(5~7명)도 청크 좌표 해시로 뽑아 복원해도 같은 수가 나온다.
///
/// 2. "걸어오는" 연출은 사용자가 실제로 조작했을 때만 재생한다.
///    PopulationManager.PopulationChanged는 세이브 복원·기아·정원 재조정 때도 발화하고
///    (SaveRestore는 건물마다 TryAssign을 재생한다), ConquestManager.RestoreExpeditions는
///    OnExpeditionSent를 재발화한다. 그래서 이 시스템은 그 이벤트들을 "다시 계산하라"는 신호로만 쓰고,
///    실제로 누가 걸어 나올지는 사용자 조작 지점이 남긴 예약(<see cref="NotifyAllocationChanged"/>,
///    <see cref="NotifyExpeditionSent"/>)으로만 결정한다. 이것이 로드할 때 성에서 인파가 쏟아지는
///    유령 캐릭터 버그를 구조적으로 막는 장치다.
/// </summary>
public sealed class VillagerDispatchSystem : MonoBehaviour
{
    private const int CREW_SIZE_MIN = 5;
    private const int CREW_SIZE_RANGE = 3;          // 5, 6, 7
    private const int CREW_SIZE_HASH_SALT = 7919;   // 인원 수와 산개 위치가 같은 해시를 쓰지 않도록 섞는 값

    private const float DEFAULT_MOVE_SPEED = 2.5f;
    private const float DEFAULT_CHEER_SECONDS = 1.5f;
    private const float DEFAULT_FADE_OUT_SECONDS = 0.4f;
    private const float DEFAULT_ATTACK_INTERVAL_SECONDS = 1f;
    private const float DEFAULT_CONQUEST_COMPLETION_IDLE_SECONDS = 1f;
    private const float DEFAULT_CONQUEST_COMPLETION_EFFECT_LIFETIME_SECONDS = 3f;
    private const float DEFAULT_EJECT_DISTANCE = 1.2f;
    private const float DEFAULT_EJECT_HEIGHT = 0.9f;
    private const float DEFAULT_EJECT_SECONDS = 0.6f;
    private const float DEFAULT_EJECT_LINGER_SECONDS = 1f;
    private const float DEFAULT_SPREAD_RADIUS = 0.45f;
    private const int DEFAULT_MAX_ACTIVE_VILLAGERS = 60;
    private const float CASTLE_SPAWN_WORLD_X = 0f;
    private const float CASTLE_SPAWN_WORLD_Y = 1f;

    private static readonly Vector3 CASTLE_SPAWN_WORLD_POSITION =
        new Vector3(CASTLE_SPAWN_WORLD_X, CASTLE_SPAWN_WORLD_Y, 0f);

    // 같은 거리라면 x+y가 작은 셀을 고른다. IsometricMath.ComputeDepthSortOrder와 같은 기준이라
    // 캐릭터가 건물 스프라이트 앞쪽에 서는 쪽으로 안정적으로 기운다.
    // FNV-1a. 크루 인원 수와 서는 자리는 이 해시로 뽑는다 - UnityEngine.Random을 쓰면 세이브를
    // 복원할 때마다 5명이 7명이 되고, GetHashCode()는 런타임/플랫폼별 안정성이 보장되지 않는다.
    // 좌표만으로 재현되므로 세이브에 저장할 것이 없다.
    // (반대로 "어떤 프리팹으로 보일지"는 일부러 무작위다 - TryPickPrefab 참고.)
    private const uint HASH_OFFSET_BASIS = 2166136261u;
    private const uint HASH_PRIME = 16777619u;
    private const int HASH_BYTE_COUNT = 4;
    private const int HASH_BITS_PER_BYTE = 8;
    private const uint HASH_BYTE_MASK = 0xFFu;

    private const float FULL_TURN_RADIANS = Mathf.PI * 2f;
    private const uint SPREAD_ANGLE_STEPS = 16u;

    [Header("참조")]
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("랜드마크에도 상주 캐릭터를 세운다. 비워두면 건물만 대상이 된다.")]
    [SerializeField] private LandmarkManager _landmarkManager;

    [Header("연출")]
    [Tooltip("겉모습 계열별 캐릭터 프리팹 묶음. 스폰할 때마다 해당 계열에서 무작위로 하나를 고른다.")]
    [SerializeField] private VillagerAppearancePool[] _appearancePools;

    [Tooltip("생성한 캐릭터를 담을 부모. 비워두면 이 오브젝트 아래에 만든다.")]
    [SerializeField] private Transform _villagerRoot;

    [SerializeField] private float _moveSpeed = DEFAULT_MOVE_SPEED;

    [Tooltip("타워에 도착한 캐릭터가 모션을 보여주고 사라지기 시작할 때까지의 시간(초).")]
    [SerializeField] private float _cheerSeconds = DEFAULT_CHEER_SECONDS;

    [Tooltip("사라질 때 투명해지는 데 걸리는 시간(초). 0이면 즉시 사라진다. 모든 소멸 경로에 적용된다.")]
    [SerializeField] private float _fadeOutSeconds = DEFAULT_FADE_OUT_SECONDS;

    [Tooltip("점령지에 선 크루가 공격 모션을 다시 재생하는 간격(초). 공격 클립 길이에 맞추면 자연스럽다.")]
    [SerializeField] private float _attackIntervalSeconds = DEFAULT_ATTACK_INTERVAL_SECONDS;

    [Tooltip("점령 완료 때 크루 위치에 한 번 재생할 이펙트 프리팹.")]
    [SerializeField] private GameObject _conquestCompletionEffectPrefab;

    [Tooltip("점령 완료 후 크루가 idle 상태로 머무는 시간(초).")]
    [SerializeField] private float _conquestCompletionIdleSeconds = DEFAULT_CONQUEST_COMPLETION_IDLE_SECONDS;

    [Tooltip("점령 완료 이펙트를 자동 제거하기까지 기다리는 시간(초).")]
    [SerializeField] private float _conquestCompletionEffectLifetimeSeconds =
        DEFAULT_CONQUEST_COMPLETION_EFFECT_LIFETIME_SECONDS;

    [Header("타워 비활성화 연출")]
    [Tooltip("타워가 비활성화된 동안 타워 위치에 띄워 둘 이펙트(Imported/Effect/Smoke 등). " +
             "타워가 다시 가동되면 자동으로 걷힌다.")]
    [SerializeField] private GameObject _towerEjectionEffectPrefab;

    [Tooltip("튕겨 나온 수비병이 날아가는 수평 거리.")]
    [SerializeField] private float _ejectDistance = DEFAULT_EJECT_DISTANCE;

    [Tooltip("튕겨 나온 수비병이 그리는 포물선의 최고 높이.")]
    [SerializeField] private float _ejectHeight = DEFAULT_EJECT_HEIGHT;

    [Tooltip("튕겨 나가 떨어지기까지 걸리는 시간(초).")]
    [SerializeField] private float _ejectSeconds = DEFAULT_EJECT_SECONDS;

    [Tooltip("떨어진 뒤 쓰러진 채로 남아 있는 시간(초). 이후 페이드아웃으로 사라진다.")]
    [SerializeField] private float _ejectLingerSeconds = DEFAULT_EJECT_LINGER_SECONDS;

    [Tooltip("같은 자리에 여럿이 설 때 흩어지는 반경. 아이소메트릭 종횡비로 눌러 화면상 원형이 된다.")]
    [SerializeField] private float _spreadRadius = DEFAULT_SPREAD_RADIUS;

    [Tooltip("동시에 존재할 수 있는 캐릭터 수 상한. 초과분은 조용히 생성하지 않는다.")]
    [SerializeField] private int _maxActiveVillagers = DEFAULT_MAX_ACTIVE_VILLAGERS;

    // 건물·랜드마크 상주. 컴포넌트 인스턴스 자체가 키다 - 좌표를 키로 쓰면 건물 이동에서 깨지고,
    // 랜드마크는 Building이 아니라 좌표 체계가 아예 다르다.
    private readonly Dictionary<IPopulationAllocationTarget, ResidentEntry> _residents = new();

    // 원정 크루. 청크당 원정은 하나뿐이므로(ConquestManager.CanSendExpedition) 청크 좌표로 식별한다
    // - ConquestPopulationCoordinator._expeditionAllocations와 같은 키 선택이다.
    private readonly Dictionary<Vector2Int, List<Villager>> _crews = new();

    // 밤 전환 등에서 한 번에 정리하기 위해 종류를 가리지 않고 전부 들고 있는다.
    private readonly List<Villager> _activeVillagers = new();

    // 사용자가 방금 조작한 대상 → 조작 직전의 배치 인원. 걸어오는 연출과 타워 방문 캐릭터는
    // 이 목록에 있는 대상에 대해서만 만든다(위 설계 2번).
    private readonly Dictionary<IPopulationAllocationTarget, int> _pendingNotify = new();

    // 사용자가 방금 발송한 원정. 같은 이유로, 크루가 성에서 걸어 나오는 것은 이 목록에 있을 때뿐이다.
    private readonly HashSet<Vector2Int> _pendingExpeditions = new();

    // 밤에 완료된 점령. 밤에는 크루가 이미 치워져 있어 돌려보낼 대상이 없으므로,
    // 좌표만 적어뒀다가 낮 전환 때 되살려 성으로 보낸다(새벽 귀환). 밤 정리에서 지우면 안 된다.
    private readonly HashSet<Vector2Int> _pendingReturns = new();

    // 밤에 성으로 들어간 상주 대상. 다음 낮에는 세이브 복원처럼 제자리에 뚝 나타나지 않고
    // 성에서 다시 걸어 나가야 하므로 대상만 남긴다. 모습은 다시 뽑아도 괜찮다.
    private readonly HashSet<IPopulationAllocationTarget> _nightShelteredResidents = new();

    // 밤에 귀가 중인 실제 캐릭터. 밤중에 다른 이벤트로 Reconcile이 다시 돌아도 중간에 Despawn하지 않는다.
    private readonly HashSet<Villager> _nightReturners = new();

    // 생산시설 상주 worker가 이번 조작의 이동 연출 1명분을 이미 담당한 경우를 기록한다.
    // 나머지 증감 인원만 이동 전용 transient로 보태기 위한 보정값이다.
    private readonly HashSet<IPopulationAllocationTarget> _residentArrivalHandledByWorker = new();
    private readonly HashSet<IPopulationAllocationTarget> _residentReturnHandledByWorker = new();

    // 타워에서 튕겨 나온 캐릭터. 밤 연출이라 밤중 정리 대상에서 빼야 한다 - 타워가 비활성화되면
    // 인구가 회수되면서 PopulationChanged가 발화하고, 그게 부른 Reconcile이 방금 만든 캐릭터를
    // 곧바로 지워버린다.
    private readonly HashSet<Villager> _ejectedVillagers = new();

    // 비활성화된 타워마다 하나씩 떠 있는 연기. 타워가 다시 가동되면 걷어낸다.
    private readonly Dictionary<Tower, Transform> _towerEjectionEffects = new();

    private readonly Dictionary<VillagerAppearance, Villager[]> _prefabsByAppearance = new();

    // 캐릭터와 이펙트는 하루에도 수십 번 생겼다 사라진다 - 매번 Instantiate/Destroy하면 GC와
    // 프리팹 인스턴스화 비용이 그대로 프레임에 실린다. 풀은 프리팹별로 통을 나누므로
    // 겉모습 무작위 추첨(TryPickPrefab)에는 아무 영향이 없다.
    private PrefabPool<Villager> _villagerPool;

    // 이펙트 프리팹은 인스펙터에 GameObject로 배선돼 있다(필드 타입을 바꾸면 기존 배선이 끊긴다).
    // 그래서 Transform을 키로 삼아 풀에 담고, 필요할 때 gameObject를 꺼내 쓴다.
    private PrefabPool<Transform> _effectPool;

    private readonly List<IPopulationAllocationTarget> _wantedResidents = new();
    private readonly List<IPopulationAllocationTarget> _residentRemovalScratch = new();
    private readonly HashSet<Vector2Int> _wantedCrews = new();
    private readonly List<Vector2Int> _crewRemovalScratch = new();

    private Vector3Int _castleCell;
    private bool _hasCastleCell;
    private bool _isReconcileQueued;

    // CycleManager가 없는 씬(팀원 테스트 씬 등)에는 밤 자체가 없으므로 낮으로 본다.
    // WorkerModeController는 조작을 막아야 해서 fail-closed지만, 여기는 연출이라 fail-open이 맞다.
    private bool IsDay =>
        _cycleManager == null || _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    private Transform VillagerRoot => _villagerRoot != null ? _villagerRoot : transform;

    private readonly struct ResidentEntry
    {
        public readonly Villager Villager;
        public readonly Vector3Int WorkCell;

        public ResidentEntry(Villager villager, Vector3Int workCell)
        {
            Villager = villager;
            WorkCell = workCell;
        }
    }

    /// <summary>겉모습 계열 하나에 쓸 프리팹 묶음. 한 계열에 여러 종을 넣으면 그 안에서 무작위로 섞인다.</summary>
    [System.Serializable]
    private struct VillagerAppearancePool
    {
        public VillagerAppearance Appearance;

        [Tooltip("이 계열에 쓸 캐릭터 프리팹들. 하나만 넣어도 되고, 여러 개 넣으면 매번 무작위로 고른다.")]
        public Villager[] Prefabs;
    }

    private void Awake()
    {
        BuildAppearanceLookup();

        _villagerPool = new PrefabPool<Villager>(VillagerRoot);
        _effectPool = new PrefabPool<Transform>(VillagerRoot);
    }

    private void BuildAppearanceLookup()
    {
        if (_appearancePools == null)
        {
            return;
        }

        foreach (VillagerAppearancePool pool in _appearancePools)
        {
            if (pool.Prefabs == null || pool.Prefabs.Length == 0)
            {
                continue;
            }

            // 같은 계열을 두 번 넣었으면 뒤엣것이 이긴다 - 인스펙터 실수를 조용히 넘기지 않도록 덮어쓴다.
            _prefabsByAppearance[pool.Appearance] = pool.Prefabs;
        }
    }

    private void OnEnable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }

        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionsChanged.AddListener(HandleExpeditionsChanged);
            _conquestManager.OnConquestCompleted.AddListener(HandleConquestCompleted);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.AddListener(HandleCycleChanged);
        }

        if (_gridMap != null)
        {
            // 작업 위치는 주변 빈 칸을 보므로 건설·철거·이동 모두 다시 계산해야 한다.
            // 추가·제거는 타워 비활성화 연출 구독도 같이 챙긴다.
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
            _gridMap.OnBuildingMoved.AddListener(HandleBuildingChanged);
        }
    }

    private void OnDisable()
    {
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }

        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionsChanged.RemoveListener(HandleExpeditionsChanged);
            _conquestManager.OnConquestCompleted.RemoveListener(HandleConquestCompleted);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnCycleChanged.RemoveListener(HandleCycleChanged);
        }

        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
            _gridMap.OnBuildingMoved.RemoveListener(HandleBuildingChanged);

            foreach (Building building in _gridMap.Buildings)
            {
                UnsubscribeTowerDisabled(building);
            }
        }

        // 그리드에서 이미 빠진 타워의 연기는 위 순회로 걷히지 않는다 - 남은 것을 전부 정리한다.
        ClearAllTowerEjectionEffects();
    }

    private void HandleBuildingAdded(Building building)
    {
        SubscribeTowerDisabled(building);
        HandleBuildingChanged(building);
    }

    private void HandleBuildingRemoving(Building building)
    {
        UnsubscribeTowerDisabled(building);
        HandleBuildingChanged(building);
    }

    private void SubscribeTowerDisabled(Building building)
    {
        if (building is Tower tower)
        {
            // UnityEvent는 같은 대상을 두 번 등록하면 두 번 호출되므로 먼저 지운다
            // (Start의 초기 훑기와 OnBuildingAdded가 겹칠 수 있다).
            tower.Disabled.RemoveListener(HandleTowerDisabled);
            tower.Disabled.AddListener(HandleTowerDisabled);
            tower.Reactivated.RemoveListener(HandleTowerReactivated);
            tower.Reactivated.AddListener(HandleTowerReactivated);
        }
    }

    private void UnsubscribeTowerDisabled(Building building)
    {
        if (building is Tower tower)
        {
            tower.Disabled.RemoveListener(HandleTowerDisabled);
            tower.Reactivated.RemoveListener(HandleTowerReactivated);

            // 철거되는 타워의 연기를 남겨두면 빈 땅에서 계속 피어오른다.
            ClearTowerEjectionEffect(tower);
        }
    }

    // 구독은 OnEnable에서 하되 첫 계산은 Start 이후로 미룬다(CLAUDE.md 이벤트 초기화 규칙).
    // 성 등록(Castle.Start)과 랜드마크 생성이 끝난 뒤에 첫 그림이 맞춰지도록 한 프레임 미뤄진다.
    private void Start()
    {
        // OnEnable 시점에는 아직 그리드에 등록되지 않은 타워가 있을 수 있다(Building들이 Start에서 등록된다).
        // OnBuildingAdded를 놓친 타워를 여기서 한 번 훑어 구독을 채운다.
        if (_gridMap != null)
        {
            foreach (Building building in _gridMap.Buildings)
            {
                SubscribeTowerDisabled(building);
            }
        }

        RequestReconcile();
    }

    /// <summary>
    /// 사용자가 인구를 배치하거나 회수했음을 알린다. <paramref name="assignedBefore"/>는
    /// 배치/회수를 시도하기 <em>직전</em>의 <see cref="IPopulationAllocationTarget.AssignedPopulation"/>이다.
    ///
    /// 실제로 몇 명이 움직였는지를 호출부가 계산하지 않는 이유: PopulationAssignmentRules가
    /// 요청량을 클램프하는 단일 출처인데, 여기서 같은 계산을 다시 하면 규칙이 두 곳으로 갈라진다.
    /// 대신 조작 전후 값의 차이를 관측해서 쓴다.
    /// </summary>
    public void NotifyAllocationChanged(IPopulationAllocationTarget target, int assignedBefore)
    {
        if (target == null)
        {
            return;
        }

        // 이미 기록이 있으면 덮어쓰지 않는다 - 한 프레임에 여러 번 눌렀을 때 첫 값이 기준이어야
        // 합계 델타가 맞는다(debounce로 reconcile은 한 번만 돈다).
        if (!_pendingNotify.ContainsKey(target))
        {
            _pendingNotify.Add(target, assignedBefore);
        }

        RequestReconcile();
    }

    /// <summary>사용자가 원정을 발송했음을 알린다. 이 호출이 있을 때만 크루가 성에서 걸어 나온다.</summary>
    public void NotifyExpeditionSent(Vector2Int chunkCoord)
    {
        _pendingExpeditions.Add(chunkCoord);
        RequestReconcile();
    }

    private void HandlePopulationChanged(PopulationState state) => RequestReconcile();

    private void HandleExpeditionsChanged() => RequestReconcile();

    private void HandleBuildingChanged(Building building) => RequestReconcile();

    private void HandleCycleChanged(CycleManager.CycleState state) => RequestReconcile();

    private void HandleConquestCompleted(Vector2Int chunkCoord)
    {
        // 낮에 완료된 경우(디버그 명령 등)에는 서 있는 크루를 그대로 돌려보낸다.
        if (_crews.ContainsKey(chunkCoord))
        {
            CompleteCrew(chunkCoord);
            return;
        }

        // 정상 경로는 여기로 온다. 점령 완료는 CycleManager.OnNightEnd에서 일어나는데, 밤이
        // 시작될 때 캐릭터를 전부 치웠으므로 돌려보낼 크루가 없다. 낮 전환 때 되살려 보낸다.
        _pendingReturns.Add(chunkCoord);
        RequestReconcile();
    }

    // 같은 프레임에 쏟아지는 이벤트를 한 번으로 접는다. 두 가지 이유 모두 필수다.
    //  1) PopulationChanged는 TryAssignClamped "안에서" 동기 발화한다. 훅은 그 호출문 다음 줄에
    //     있으므로, 즉시 계산하면 아직 예약(_pendingNotify)이 없어 걸어오는 연출이 영영 안 나온다.
    //  2) 세이브 복원은 건물마다 TryAssign을 재생해(SaveRestore) 이벤트를 수십 발 쏜다.
    private void RequestReconcile()
    {
        if (_isReconcileQueued)
        {
            return;
        }

        _isReconcileQueued = true;
        ReconcileNextFrameAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid ReconcileNextFrameAsync(CancellationToken token)
    {
        await UniTask.Yield(PlayerLoopTiming.Update, token);

        _isReconcileQueued = false;
        Reconcile();
    }

    private void Reconcile()
    {
        // 연출은 없어도 게임이 돌아야 하므로 막지 않고 건너뛴다. WiringGuard가 (인스턴스, 멤버)당
        // 한 번만 보고하므로 매 프레임 콘솔이 채워지지 않는다.
        if (!WiringGuard.Optional(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        if (_prefabsByAppearance.Count == 0)
        {
            WiringGuard.Optional(null, nameof(_appearancePools), this);
            return;
        }

        // 밤에는 생산시설 쪽 상주만 성으로 걸어 돌아가고, 점령 크루와 타워 transient는 기존처럼 정리한다.
        // 이 가드가 없으면 밤에 도는 PopulationChanged(기아 정산 등)가 상주를 되살린다.
        // _pendingReturns는 일부러 비우지 않는다 - 밤에 완료된 점령의 귀환이 여기 담겨 있다.
        if (!IsDay)
        {
            ReconcileNight();
            _pendingNotify.Clear();
            _pendingExpeditions.Clear();
            return;
        }

        ProcessPendingReturns();
        ReconcileResidents();
        ReconcileCrews();
        SpawnPendingTransients();

        _pendingNotify.Clear();
        _pendingExpeditions.Clear();
    }

    // 밤 사이에 완료된 점령의 크루를 청크에 되살려 성으로 걸려 보낸다.
    private void ProcessPendingReturns()
    {
        if (_pendingReturns.Count == 0)
        {
            return;
        }

        if (!TryResolveCastleCell(out Vector3Int castleCell))
        {
            _pendingReturns.Clear();
            return;
        }

        foreach (Vector2Int chunkCoord in _pendingReturns)
        {
            // 떠날 때 모여 있던 그 자리에서 출발해야 이어지는 그림이 된다 - SpawnCrew와 같은 기준 칸을 쓴다.
            if (!TryResolveCrewAnchorCell(chunkCoord, out Vector3Int originCell))
            {
                continue;
            }

            int crewSize = ResolveCrewSize(chunkCoord);
            SpawnConquestCompletionEffect(originCell);

            for (int i = 0; i < crewSize; i++)
            {
                Villager villager = Spawn(
                    new VillagerOrder(
                        VillagerProfile.Expedition,
                        originCell,
                        originCell,
                        castleCell,
                        ResolveRingOffset(i, crewSize),
                        hasOutboundLeg: false,
                        hasFixedOriginWorldPosition: false,
                        originWorldPosition: default(Vector3),
                        hasFixedCastleWorldPosition: true,
                        castleWorldPosition: CASTLE_SPAWN_WORLD_POSITION),
                    VillagerAppearance.Expedition);

                villager?.SendHomeAfterIdle(_conquestCompletionIdleSeconds);
            }
        }

        _pendingReturns.Clear();
    }

    private void ReconcileResidents()
    {
        _residentArrivalHandledByWorker.Clear();
        _residentReturnHandledByWorker.Clear();

        CollectWantedResidents();
        PruneNightShelteredResidents();

        _residentRemovalScratch.Clear();

        foreach (KeyValuePair<IPopulationAllocationTarget, ResidentEntry> pair in _residents)
        {
            if (IsResidentStale(pair.Key, pair.Value))
            {
                _residentRemovalScratch.Add(pair.Key);
            }
        }

        foreach (IPopulationAllocationTarget target in _residentRemovalScratch)
        {
            ResidentEntry entry = _residents[target];

            // 캐릭터가 이미 파괴된 것도 걷어낼 대상에 포함되므로(IsResidentStale) 널 검사가 필요하다.
            if (entry.Villager != null)
            {
                // 일이 끝나 물러나는 것이면 그 자리에서 일하던 그 캐릭터가 직접 성으로 걸어간다.
                if (IsRetiringResident(target, entry))
                {
                    entry.Villager.SendHome();

                    if (IsProductionFacilityTarget(target))
                    {
                        _residentReturnHandledByWorker.Add(target);
                    }
                }
                else
                {
                    entry.Villager.Despawn();
                }
            }

            _residents.Remove(target);
        }

        if (!TryResolveCastleCell(out Vector3Int castleCell))
        {
            return;
        }

        foreach (IPopulationAllocationTarget target in _wantedResidents)
        {
            if (_residents.ContainsKey(target) || !TryResolveWorkCell(target, out Vector3Int workCell))
            {
                continue;
            }

            bool shouldWalkFromCastle =
                _pendingNotify.ContainsKey(target) ||
                _nightShelteredResidents.Contains(target);

            // 사용자가 방금 배치했거나 밤에 성으로 들어갔다가 낮에 복귀하는 경우에는 성에서 걸어온다.
            // 그 외 세이브 복원 등은 제자리에서 일하는 상태로 등장한다.
            Villager villager = Spawn(
                new VillagerOrder(
                    VillagerProfile.Resident,
                    castleCell,
                    workCell,
                    castleCell,
                    Vector3.zero,
                    hasOutboundLeg: shouldWalkFromCastle,
                    hasFixedOriginWorldPosition: true,
                    originWorldPosition: CASTLE_SPAWN_WORLD_POSITION,
                    hasFixedCastleWorldPosition: true,
                    castleWorldPosition: CASTLE_SPAWN_WORLD_POSITION),
                VillagerAppearance.Worker);

            if (villager != null)
            {
                _residents.Add(target, new ResidentEntry(villager, workCell));

                if (shouldWalkFromCastle &&
                    _pendingNotify.ContainsKey(target) &&
                    IsProductionFacilityTarget(target))
                {
                    _residentArrivalHandledByWorker.Add(target);
                }

                _nightShelteredResidents.Remove(target);
            }
        }
    }

    private void CompleteCrew(Vector2Int chunkCoord)
    {
        if (!_crews.TryGetValue(chunkCoord, out List<Villager> crew))
        {
            return;
        }

        if (TryResolveCrewAnchorCell(chunkCoord, out Vector3Int anchorCell))
        {
            SpawnConquestCompletionEffect(anchorCell);
        }

        foreach (Villager villager in crew)
        {
            if (villager != null)
            {
                villager.SendHomeAfterIdle(_conquestCompletionIdleSeconds);
            }
        }

        _crews.Remove(chunkCoord);
    }

    // 밤 전투 중 타워가 비활성화될 때, 수비병 하나가 사망 모션으로 튕겨 나오고 연기가 피어오른다.
    // Reconcile을 거치지 않고 곧바로 만든다 - 밤에는 Reconcile이 전원 정리로 끝나므로 그 경로로는
    // 이 연출을 낼 수 없다. 대신 _ejectedVillagers에 넣어 밤중 정리에서 제외한다.
    private void HandleTowerDisabled(Tower tower)
    {
        if (tower == null || _prefabsByAppearance.Count == 0)
        {
            return;
        }

        Vector3 towerPosition = WorkerCountOverlayRenderer.ResolveLabelPosition(tower);

        SpawnTowerEjectionEffect(tower, towerPosition);

        // 인원수와 무관하게 한 명만 튀어나온다 - 연출이 목적이지 인구를 표현하는 것이 아니다.
        Villager villager = Spawn(
            new VillagerOrder(
                VillagerProfile.Ejected,
                _castleCell,
                _castleCell,
                _castleCell,
                ResolveEjectOffset(),
                hasOutboundLeg: false,
                hasFixedOriginWorldPosition: true,
                originWorldPosition: towerPosition,
                hasFixedCastleWorldPosition: false,
                castleWorldPosition: default),
            VillagerAppearance.Soldier);

        if (villager != null)
        {
            villager.ConstructEject(_ejectHeight, _ejectSeconds, _ejectLingerSeconds);
            _ejectedVillagers.Add(villager);
        }
    }

    // 튕겨 나가는 방향은 매번 달라야 자연스럽다. 아이소메트릭이라 세로를 눌러 화면상 원형으로 흩어진다.
    private Vector3 ResolveEjectOffset()
    {
        float angle = Random.Range(0f, FULL_TURN_RADIANS);

        return new Vector3(
            Mathf.Cos(angle) * _ejectDistance,
            Mathf.Sin(angle) * _ejectDistance * IsometricMath.RADIUS_Y_RATIO,
            0f);
    }

    // 연기는 타워가 다시 가동될 때까지 계속 피어오른다 - 시간이 아니라 타워 상태가 수명을 정한다.
    // Smoke 프리팹이 looping 파티클이라 스스로 멈추지 않으므로, 반드시 짝이 되는 정리(HandleTowerReactivated /
    // UnsubscribeTowerDisabled / OnDisable)가 있어야 빈 땅에 연기가 남지 않는다.
    private void SpawnTowerEjectionEffect(Tower tower, Vector3 worldPosition)
    {
        if (_towerEjectionEffectPrefab == null)
        {
            return;
        }

        // 같은 타워가 다시 비활성화되는 경우(부활 후 재파괴) 이전 연기를 먼저 걷는다.
        ClearTowerEjectionEffect(tower);

        Transform effect = AcquireEffect(_towerEjectionEffectPrefab, worldPosition);

        if (effect != null)
        {
            _towerEjectionEffects[tower] = effect;
        }
    }

    private void HandleTowerReactivated(Tower tower)
    {
        ClearTowerEjectionEffect(tower);
    }

    private void ClearTowerEjectionEffect(Tower tower)
    {
        if (tower == null || !_towerEjectionEffects.TryGetValue(tower, out Transform effect))
        {
            return;
        }

        ReleaseEffect(effect);
        _towerEjectionEffects.Remove(tower);
    }

    private void ClearAllTowerEjectionEffects()
    {
        foreach (Transform effect in _towerEjectionEffects.Values)
        {
            ReleaseEffect(effect);
        }

        _towerEjectionEffects.Clear();
    }

    private void SpawnConquestCompletionEffect(Vector3Int cell)
    {
        if (_gridMap == null)
        {
            return;
        }

        Transform effect = AcquireEffect(
            _conquestCompletionEffectPrefab, _gridMap.ConvertGridToWorld(cell));

        // 수명이 0 이하이면 스스로 걷히지 않는다(예전 Destroy(effect, 0) 시절과 같은 동작).
        if (effect != null && _conquestCompletionEffectLifetimeSeconds > 0f)
        {
            ReleaseEffectAfterAsync(
                effect,
                _conquestCompletionEffectLifetimeSeconds,
                this.GetCancellationTokenOnDestroy()).Forget();
        }
    }

    private async UniTaskVoid ReleaseEffectAfterAsync(
        Transform effect, float delaySeconds, CancellationToken token)
    {
        await UniTask.WaitForSeconds(delaySeconds, cancellationToken: token);

        ReleaseEffect(effect);
    }

    // 이펙트는 파티클이라 재사용할 때 반드시 되감아야 한다. 비활성화만으로는 재생 위치가
    // 정해지지 않는다(playOnAwake가 꺼져 있으면 멈춘 채로 다시 나타난다).
    private Transform AcquireEffect(GameObject prefab, Vector3 worldPosition)
    {
        if (prefab == null)
        {
            return null;
        }

        Transform effect = _effectPool.Acquire(prefab.transform);

        if (effect == null)
        {
            return null;
        }

        effect.SetPositionAndRotation(worldPosition, Quaternion.identity);

        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Clear(true);
            particles.Play(true);
        }

        return effect;
    }

    private void ReleaseEffect(Transform effect)
    {
        if (effect == null)
        {
            return;
        }

        // 남아 있던 파티클을 지우지 않으면 다음에 꺼내 쓸 때 이전 자리의 잔상이 한 프레임 비친다.
        foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        _effectPool.Release(effect);
    }

    private void ReconcileNight()
    {
        SendResidentsHomeForNight();
        DespawnNightTransientVillagers();
        _crews.Clear();
    }

    private void SendResidentsHomeForNight()
    {
        foreach (KeyValuePair<IPopulationAllocationTarget, ResidentEntry> pair in _residents)
        {
            if (IsResidentTarget(pair.Key))
            {
                _nightShelteredResidents.Add(pair.Key);
            }

            Villager villager = pair.Value.Villager;

            if (villager == null)
            {
                continue;
            }

            _nightReturners.Add(villager);
            villager.SendHome();
        }

        _residents.Clear();
    }

    private void DespawnNightTransientVillagers()
    {
        for (int i = _activeVillagers.Count - 1; i >= 0; i--)
        {
            Villager villager = _activeVillagers[i];

            if (villager == null ||
                _nightReturners.Contains(villager) ||
                _ejectedVillagers.Contains(villager))
            {
                continue;
            }

            villager.Despawn();
        }
    }

    private void PruneNightShelteredResidents()
    {
        if (_nightShelteredResidents.Count == 0)
        {
            return;
        }

        _residentRemovalScratch.Clear();

        foreach (IPopulationAllocationTarget target in _nightShelteredResidents)
        {
            if (!IsResidentTarget(target))
            {
                _residentRemovalScratch.Add(target);
            }
        }

        foreach (IPopulationAllocationTarget target in _residentRemovalScratch)
        {
            _nightShelteredResidents.Remove(target);
        }
    }

    private void CollectWantedResidents()
    {
        _wantedResidents.Clear();

        foreach (Building building in _gridMap.Buildings)
        {
            var target = building.GetComponent<IPopulationAllocationTarget>();

            if (IsResidentTarget(target))
            {
                _wantedResidents.Add(target);
            }
        }

        if (_landmarkManager == null)
        {
            return;
        }

        foreach (Landmark landmark in _landmarkManager.Landmarks)
        {
            // 점령하지 않은 랜드마크에는 애초에 인구를 넣을 수 없다(LandmarkPopulation과 같은 기준).
            if (landmark.IsConquered && IsResidentTarget(landmark.Population))
            {
                _wantedResidents.Add(landmark.Population);
            }
        }
    }

    // 타워는 상주 대상이 아니다 - 도착해서 모션 한 번 하고 사라지는 transient로 따로 처리한다.
    // TowerPopulation 타입으로 거르는 이유: IPopulationAllocationTarget에 배정 타입을 추가하면
    // 구현체 네 개를 전부 건드려야 한다. 타워 파생 클래스가 생기면 자동으로 커버되는 방향이라 안전하다.
    private static bool IsResidentTarget(IPopulationAllocationTarget target) =>
        IsAlive(target) &&
        target is not TowerPopulation &&
        target.IsInitialized &&
        target.AssignedPopulation > 0;

    private static bool IsProductionFacilityTarget(IPopulationAllocationTarget target) =>
        target is FactoryPopulation;

    // 상주가 "일을 마치고 물러나는" 경우인지. 이때만 성으로 걸어 돌아가는 연출을 붙인다.
    // 건물이 철거됐거나(대상이 사라짐) 자리를 옮긴 경우(건물 이동)에는 걸어갈 맥락이 없으므로
    // 그 자리에서 조용히 사라져야 한다.
    private bool IsRetiringResident(IPopulationAllocationTarget target, ResidentEntry entry)
    {
        return IsAlive(target) &&
            target.AssignedPopulation == 0 &&
            TryResolveWorkCell(target, out Vector3Int workCell) &&
            workCell == entry.WorkCell;
    }

    private bool IsResidentStale(IPopulationAllocationTarget target, ResidentEntry entry)
    {
        if (!IsAlive(target) || entry.Villager == null || !_wantedResidents.Contains(target))
        {
            return true;
        }

        // 건물이 이동했으면 옛 자리의 상주를 걷어내고 새 자리에 다시 세운다.
        return !TryResolveWorkCell(target, out Vector3Int workCell) || workCell != entry.WorkCell;
    }

    private void ReconcileCrews()
    {
        _wantedCrews.Clear();

        if (_conquestManager != null)
        {
            foreach (ConquestExpedition expedition in _conquestManager.ActiveExpeditions)
            {
                _wantedCrews.Add(expedition.TargetChunkCoord);
            }
        }

        _crewRemovalScratch.Clear();

        foreach (KeyValuePair<Vector2Int, List<Villager>> pair in _crews)
        {
            if (!_wantedCrews.Contains(pair.Key))
            {
                _crewRemovalScratch.Add(pair.Key);
            }
        }

        foreach (Vector2Int chunkCoord in _crewRemovalScratch)
        {
            RemoveCrew(chunkCoord, sendHome: false);
        }

        foreach (Vector2Int chunkCoord in _wantedCrews)
        {
            if (!_crews.ContainsKey(chunkCoord))
            {
                SpawnCrew(chunkCoord, hasOutboundLeg: _pendingExpeditions.Contains(chunkCoord));
            }
        }
    }

    private void SpawnCrew(Vector2Int chunkCoord, bool hasOutboundLeg)
    {
        if (!TryResolveCastleCell(out Vector3Int castleCell))
        {
            return;
        }

        // 등록되지 않은 청크나 육지가 없는 청크는 연출을 건너뛴다 - GetChunkCenterWorld의
        // "미등록이면 Vector3.zero" 실패 모드를 아예 밟지 않는다.
        if (!TryResolveCrewAnchorCell(chunkCoord, out Vector3Int workCell))
        {
            return;
        }

        int crewSize = ResolveCrewSize(chunkCoord);
        var crew = new List<Villager>(crewSize);

        for (int i = 0; i < crewSize; i++)
        {
            Villager villager = Spawn(
                new VillagerOrder(
                    VillagerProfile.Expedition,
                    castleCell,
                    workCell,
                    castleCell,
                    ResolveRingOffset(i, crewSize),
                    hasOutboundLeg,
                    hasFixedOriginWorldPosition: true,
                    originWorldPosition: CASTLE_SPAWN_WORLD_POSITION,
                    hasFixedCastleWorldPosition: true,
                    castleWorldPosition: CASTLE_SPAWN_WORLD_POSITION),
                VillagerAppearance.Expedition);

            if (villager != null)
            {
                crew.Add(villager);
            }
        }

        if (crew.Count > 0)
        {
            _crews.Add(chunkCoord, crew);
        }
    }

    private void RemoveCrew(Vector2Int chunkCoord, bool sendHome)
    {
        if (!_crews.TryGetValue(chunkCoord, out List<Villager> crew))
        {
            return;
        }

        foreach (Villager villager in crew)
        {
            if (villager == null)
            {
                continue;
            }

            if (sendHome)
            {
                villager.SendHome();
            }
            else
            {
                villager.Despawn();
            }
        }

        _crews.Remove(chunkCoord);
    }

    // 사용자가 방금 조작한 대상에 대해서만, 조작 전후 배치 인원의 차이만큼 캐릭터를 내보낸다.
    // 이 목록에 없는 대상은 절대 캐릭터를 만들지 않는다 - 세이브 복원이 쏘는 PopulationChanged에
    // 반응해 유인 타워마다 인파가 쏟아지는 것을 막는 핵심 가드다.
    private void SpawnPendingTransients()
    {
        if (_pendingNotify.Count == 0 || !TryResolveCastleCell(out Vector3Int castleCell))
        {
            return;
        }

        foreach (KeyValuePair<IPopulationAllocationTarget, int> pair in _pendingNotify)
        {
            IPopulationAllocationTarget target = pair.Key;

            if (!IsAlive(target) || !TryResolveWorkCell(target, out Vector3Int workCell))
            {
                continue;
            }

            bool isTower = target is TowerPopulation;
            bool isProductionFacility = IsProductionFacilityTarget(target);

            if (!isTower && !isProductionFacility)
            {
                continue;
            }

            int delta = target.AssignedPopulation - pair.Value;

            if (delta < 0)
            {
                int count = ResolveTransientCount(
                    -delta,
                    isProductionFacility && _residentReturnHandledByWorker.Contains(target));

                if (count <= 0)
                {
                    continue;
                }

                // 회수 - 빠져나온 인원만큼 일터에서 성으로 돌아간다.
                SpawnGroup(
                    VillagerProfile.Recall, workCell, castleCell, castleCell,
                    count, workCell, ResolveAppearance(target));
            }
            else if (delta > 0)
            {
                int count = ResolveTransientCount(
                    delta,
                    isProductionFacility && _residentArrivalHandledByWorker.Contains(target));

                if (count <= 0)
                {
                    continue;
                }

                // 배치 - 타워는 도착 모션, 생산시설의 추가 인원은 이동만 보여주고 사라진다.
                SpawnGroup(
                    isTower ? VillagerProfile.TowerVisit : VillagerProfile.Transit,
                    castleCell,
                    workCell,
                    castleCell,
                    count,
                    workCell,
                    ResolveAppearance(target));
            }
        }
    }

    private static int ResolveTransientCount(int requestedCount, bool isHandledByResidentWorker) =>
        Mathf.Max(0, requestedCount - (isHandledByResidentWorker ? 1 : 0));

    private static VillagerAppearance ResolveAppearance(IPopulationAllocationTarget target) =>
        target is TowerPopulation ? VillagerAppearance.Soldier : VillagerAppearance.Worker;

    private void SpawnGroup(
        VillagerProfile profile,
        Vector3Int originCell,
        Vector3Int destinationCell,
        Vector3Int castleCell,
        int count,
        Vector3Int hashSeedCell,
        VillagerAppearance appearance)
    {
        for (int i = 0; i < count; i++)
        {
            Spawn(
                new VillagerOrder(
                    profile,
                    originCell,
                    destinationCell,
                    castleCell,
                    ResolveSpreadOffset(StableHash(hashSeedCell.x, hashSeedCell.y, i)),
                    hasOutboundLeg: true,
                    hasFixedOriginWorldPosition: originCell == castleCell,
                    originWorldPosition: CASTLE_SPAWN_WORLD_POSITION,
                    hasFixedCastleWorldPosition: destinationCell == castleCell,
                    castleWorldPosition: CASTLE_SPAWN_WORLD_POSITION),
                appearance);
        }
    }

    private Villager Spawn(in VillagerOrder order, VillagerAppearance appearance)
    {
        if (_activeVillagers.Count >= _maxActiveVillagers ||
            !TryPickPrefab(appearance, out Villager prefab))
        {
            return null;
        }

        Villager villager = _villagerPool.Acquire(prefab);

        if (villager == null)
        {
            return null;
        }

        // 지난 생애의 알파·애니메이터 적용 기록·이동 상태를 먼저 지운다. Dispatch보다 앞서야 한다.
        villager.ResetForSpawn();

        // Dispatch가 즉시 끝나 Finished를 부르는 경우에도 목록이 어긋나지 않도록 먼저 등록한다.
        _activeVillagers.Add(villager);
        villager.Finished += HandleVillagerFinished;

        villager.Construct(_gridMap, _moveSpeed, _cheerSeconds, _fadeOutSeconds, _attackIntervalSeconds);
        villager.Dispatch(order);

        return villager;
    }

    // 겉모습은 크루 인원 수와 달리 결정론적이지 않다 - 스폰할 때마다 무작위로 고른다.
    // 그래서 세이브를 다시 불러오거나 날이 바뀌면 같은 자리에 다른 사람이 서 있을 수 있다(의도된 동작).
    // 인원 수는 여전히 좌표 해시로 뽑으므로 복원해도 5명이 7명이 되지는 않는다.
    private bool TryPickPrefab(VillagerAppearance appearance, out Villager prefab)
    {
        prefab = null;

        if (!_prefabsByAppearance.TryGetValue(appearance, out Villager[] prefabs))
        {
            return false;
        }

        prefab = prefabs[Random.Range(0, prefabs.Length)];

        // 배열 중간에 빈 칸을 남겨둔 경우를 걸러낸다 - 인스펙터에서 Size만 늘리면 널이 들어간다.
        return prefab != null;
    }

    // 반납은 반드시 추적 목록에서 빼낸 다음에 한다 - 다음 획득이 같은 인스턴스를 돌려주므로,
    // 순서가 뒤집히면 새 생애의 캐릭터가 이전 생애의 집합(밤 귀가자·튕겨 나온 병사)에 남아 있게 된다.
    private void HandleVillagerFinished(Villager villager)
    {
        _nightReturners.Remove(villager);
        _ejectedVillagers.Remove(villager);
        villager.Finished -= HandleVillagerFinished;
        _activeVillagers.Remove(villager);

        // 씬 언로드 등으로 파괴되면서 알려온 경우다 - 통에 넣으면 죽은 오브젝트가 섞인다.
        if (villager.WasDestroyed)
        {
            _villagerPool.Forget(villager);
            return;
        }

        _villagerPool.Release(villager);
    }

    private void DespawnAll()
    {
        for (int i = _activeVillagers.Count - 1; i >= 0; i--)
        {
            if (_activeVillagers[i] != null)
            {
                _activeVillagers[i].Despawn();
            }
        }

        _residents.Clear();
        _crews.Clear();
        _nightShelteredResidents.Clear();
        _nightReturners.Clear();
    }

    // 인터페이스 참조로는 Unity의 == 오버로드를 타지 않아 파괴된 컴포넌트를 "살아 있다"고 오판한다
    // (WiringGuard.RequireRef가 같은 이유로 명시적 캐스팅을 한다).
    private static bool IsAlive(IPopulationAllocationTarget target) =>
        target is Component component && component != null;

    private bool TryResolveWorkCell(IPopulationAllocationTarget target, out Vector3Int cell)
    {
        cell = default;

        if (target is not Component component || component == null)
        {
            return false;
        }

        if (component.TryGetComponent(out Building building))
        {
            cell = ResolveBuildingWorkCell(building);
            return true;
        }

        // 랜드마크는 Building이 아니라 발자국이 없다 - 실제 마커 위치에 가장 가까운 육지 칸을 자리로 삼는다.
        if (component.TryGetComponent(out Landmark landmark))
        {
            return TryPickClosestChunkLandCell(
                landmark.ChunkCoord,
                landmark.transform.position,
                requireEmpty: true,
                out cell);
        }

        return false;
    }

    // 건물마다 footprint와 회전, 시각 오프셋이 다르므로 앵커 고정 오프셋을 쓰지 않는다.
    // 실제 footprint 주변의 빈 육지 칸 중 스프라이트 중심에 가장 가까운 자리를 고른다.
    private Vector3Int ResolveBuildingWorkCell(Building building)
    {
        if (TryPickBuildingPerimeterCell(building, building.transform.position, out Vector3Int workCell))
        {
            return workCell;
        }

        // 주변에 빈 육지가 없으면 건물 footprint 안쪽 중 중심에 가까운 칸으로 물 위 스폰만 피한다.
        IReadOnlyList<Vector3Int> footprint = _gridMap.GetFootprintCoords(building);
        if (TryPickClosestCell(footprint, building.transform.position, requireEmpty: false, out workCell))
        {
            return workCell;
        }

        return building.PlacementAnchor;
    }

    // 성의 위치는 그리드 등록(Castle.Start)에서만 확정된다. Start끼리는 순서가 보장되지 않으므로
    // 첫 사용 시점에 조회해 캐싱한다. Castle.transform.position은 인스펙터에서 손으로 맞춘 값이라
    // 그리드 앵커와 동기화되지 않으므로 절대 쓰면 안 된다(Castle.cs 주석 참고).
    private bool TryResolveCastleCell(out Vector3Int cell)
    {
        if (_hasCastleCell)
        {
            cell = _castleCell;
            return true;
        }

        Castle castle = _gridMap.FindBuilding<Castle>(out Vector3Int occupiedCoord);

        if (castle == null)
        {
            cell = default;
            return false;
        }

        Vector3Int centerCell = castle.PlacementAnchor + castle.FootprintShape.CenterOffset;
        Vector3 centerWorld = _gridMap.ConvertGridToWorld(centerCell);

        if (TryPickBuildingPerimeterCell(castle, centerWorld, out Vector3Int exitCell))
        {
            _castleCell = exitCell;
        }
        else if (IsUsableLandCell(centerCell, requireEmpty: false))
        {
            _castleCell = centerCell;
        }
        else
        {
            _castleCell = occupiedCoord;
        }

        _hasCastleCell = true;
        cell = _castleCell;
        return true;
    }

    private bool TryPickBuildingPerimeterCell(
        Building building,
        Vector3 targetWorld,
        out Vector3Int cell)
    {
        cell = default;

        IReadOnlyList<Vector3Int> footprint = _gridMap.GetFootprintCoords(building);
        if (footprint.Count == 0)
        {
            return false;
        }

        var footprintSet = new HashSet<Vector3Int>(footprint);
        var candidates = new HashSet<Vector3Int>();

        foreach (Vector3Int footprintCell in footprint)
        {
            foreach (Vector3Int direction in CellDirections.ALL_EIGHT)
            {
                Vector3Int candidate = footprintCell + direction;

                if (!footprintSet.Contains(candidate))
                {
                    candidates.Add(candidate);
                }
            }
        }

        return TryPickClosestCell(candidates, targetWorld, requireEmpty: true, out cell);
    }

    private bool TryPickClosestChunkLandCell(
        Vector2Int chunkCoord,
        Vector3 targetWorld,
        bool requireEmpty,
        out Vector3Int cell)
    {
        cell = default;

        Chunk chunk = _gridMap.GetChunk(chunkCoord);
        return chunk != null &&
            TryPickClosestCell(chunk.LandCellCoords, targetWorld, requireEmpty, out cell);
    }

    private bool TryPickClosestCell(
        IEnumerable<Vector3Int> candidates,
        Vector3 targetWorld,
        bool requireEmpty,
        out Vector3Int cell)
    {
        cell = default;
        bool hasBest = false;
        float bestDistanceSqr = 0f;

        foreach (Vector3Int candidate in candidates)
        {
            if (!IsUsableLandCell(candidate, requireEmpty))
            {
                continue;
            }

            float distanceSqr = (_gridMap.ConvertGridToWorld(candidate) - targetWorld).sqrMagnitude;

            if (!hasBest || IsBetterInteractionCell(candidate, distanceSqr, cell, bestDistanceSqr))
            {
                cell = candidate;
                bestDistanceSqr = distanceSqr;
                hasBest = true;
            }
        }

        return hasBest;
    }

    private bool IsUsableLandCell(Vector3Int cell, bool requireEmpty)
    {
        Chunk chunk = _gridMap.GetChunkAt(cell);
        return chunk != null &&
            chunk.ContainsLandCell(cell) &&
            (!requireEmpty || _gridMap.GetBuildingAt(cell) == null);
    }

    private static bool IsBetterInteractionCell(
        Vector3Int candidate,
        float candidateDistanceSqr,
        Vector3Int best,
        float bestDistanceSqr)
    {
        if (candidateDistanceSqr < bestDistanceSqr)
        {
            return true;
        }

        if (!Mathf.Approximately(candidateDistanceSqr, bestDistanceSqr))
        {
            return false;
        }

        int candidateFrontScore = ResolveFrontCellScore(candidate);
        int bestFrontScore = ResolveFrontCellScore(best);

        if (candidateFrontScore != bestFrontScore)
        {
            return candidateFrontScore < bestFrontScore;
        }

        if (candidate.x != best.x)
        {
            return candidate.x < best.x;
        }

        return candidate.y < best.y;
    }

    private static int ResolveFrontCellScore(Vector3Int cell) =>
        cell.x + cell.y;

    // 크루 전원이 모여 설 기준 칸. 청크의 육지 칸 중 "가운데에 가장 가까운" 칸을 고른다.
    // 예전처럼 인원마다 다른 칸을 고르면 청크 전체(수십 칸)에 흩어져 한 무리로 안 보인다.
    // 셀 좌표 평균으로 중심을 잡는 이유: 월드 좌표(GetChunkCenterWorld)는 고저차가 섞여 있어
    // 언덕이 낀 청크에서 중심이 밀린다.
    private bool TryResolveCrewAnchorCell(Vector2Int chunkCoord, out Vector3Int cell)
    {
        cell = default;

        Chunk chunk = _gridMap.GetChunk(chunkCoord);

        if (chunk == null || chunk.LandCellCoords.Count == 0)
        {
            return false;
        }

        Vector3 sum = Vector3.zero;

        foreach (Vector3Int coord in chunk.LandCellCoords)
        {
            sum += coord;
        }

        Vector3 center = sum / chunk.LandCellCoords.Count;
        float bestDistanceSqr = float.MaxValue;

        foreach (Vector3Int coord in chunk.LandCellCoords)
        {
            float distanceSqr = ((Vector3)coord - center).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                cell = coord;
            }
        }

        return true;
    }

    // 한 칸에 여럿이 설 때 겹치지 않도록 고르게 원형으로 벌린다. 해시로 각도를 뽑으면 같은 각이
    // 겹쳐 두 명이 포개지므로, 인원 수를 아는 크루는 인덱스로 균등 배치한다.
    private Vector3 ResolveRingOffset(int index, int count)
    {
        if (count <= 1)
        {
            return Vector3.zero;
        }

        float angle = index * (FULL_TURN_RADIANS / count);

        return new Vector3(
            Mathf.Cos(angle) * _spreadRadius,
            Mathf.Sin(angle) * _spreadRadius * IsometricMath.RADIUS_Y_RATIO,
            0f);
    }

    private static int ResolveCrewSize(Vector2Int chunkCoord) =>
        CREW_SIZE_MIN + (int)(StableHash(chunkCoord.x, chunkCoord.y, CREW_SIZE_HASH_SALT) % CREW_SIZE_RANGE);

    private Vector3 ResolveSpreadOffset(uint hash)
    {
        float angle = hash % SPREAD_ANGLE_STEPS * (FULL_TURN_RADIANS / SPREAD_ANGLE_STEPS);

        // 아이소메트릭 종횡비로 Y를 눌러 화면상 원형으로 흩어지게 한다(사거리 판정이 타원인 것과 같은 이유).
        return new Vector3(
            Mathf.Cos(angle) * _spreadRadius,
            Mathf.Sin(angle) * _spreadRadius * IsometricMath.RADIUS_Y_RATIO,
            0f);
    }

    private static uint StableHash(int first, int second, int third)
    {
        uint hash = HASH_OFFSET_BASIS;

        hash = MixInt(hash, first);
        hash = MixInt(hash, second);
        hash = MixInt(hash, third);

        return hash;
    }

    private static uint MixInt(uint hash, int value)
    {
        var bits = (uint)value;

        for (int i = 0; i < HASH_BYTE_COUNT; i++)
        {
            hash ^= (bits >> (i * HASH_BITS_PER_BYTE)) & HASH_BYTE_MASK;
            hash *= HASH_PRIME;
        }

        return hash;
    }
}
