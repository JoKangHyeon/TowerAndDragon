using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ChunkLandmarkTable을 읽어 맵에 랜드마크를 세우고, 점령된 랜드마크의 보상을 지급한다.
///
/// 점령 감지에 ConquestManager.OnConquestCompleted가 아니라 GridMap.OnChunkStateChanged를 쓴다.
/// OnConquestCompleted는 원정으로 점령한 청크에서만 발화하고,
/// ConquestManager.AnnexUnregisteredLandNeighbors가 흡수하는 "자투리" 청크에서는 발화하지 않는다.
/// 그 경로로 편입된 랜드마크는 보상이 영원히 지급되지 않으므로, 상태 변화 자체를 보고 훑는다.
/// 랜드마크 수는 많아야 수십 개라 매 상태 변화마다 전체를 훑어도 부담이 없다.
///
/// 그 대신 세이브 복원(ConquestManager.RestoreTerritory)도 같은 이벤트를 발화시키므로,
/// 수령 이력 복원(RestoreClaims)이 영토 복원보다 반드시 먼저 일어나야 한다. SaveRestore.Apply 참고.
/// </summary>
public sealed class LandmarkManager : MonoBehaviour, ILandmarkOwnershipQuery
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private ChunkLandmarkTable _landmarkTable;

    [Header("보상 지급 대상")]
    [SerializeField] private DragonEggInventorySystem _eggInventory;
    [SerializeField] private ResourceManager _resourceManager;

    [Header("인구 가동")]
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("랜드마크 시각물을 담을 부모. 비우면 이 오브젝트 아래에 만든다.")]
    [WiringOptional]
    [SerializeField] private Transform _landmarkRoot;

    [Header("마커")]
    [Tooltip("랜드마크 위에 띄울 아이콘 프리팹. 비우면 마커 없이 시각물만 생성한다.")]
    [SerializeField] private LandmarkMarker _markerPrefab;

    [SerializeField] private float _markerYOffset = 1f;

    private readonly List<Landmark> _landmarks = new();
    private readonly HashSet<string> _claimedLandmarkIds = new();

    // 랜드마크가 새로 수령되었을 때. 연구 UI·토스트가 갱신 시점으로 쓴다.
    public UnityEvent<LandmarkDataSO> OnLandmarkClaimed = new();

    // 가동 상태(배치 인구)가 바뀌었을 때. LandmarkOperationCoordinator가 생산 배율을 다시 계산한다.
    public UnityEvent OnLandmarkOperationChanged = new();

    public IReadOnlyList<Landmark> Landmarks => _landmarks;

    private bool _isReady;

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnChunkStateChanged.AddListener(RefreshConquestState);
        }

        // 기아로 인구가 빠질 때는 LandmarkPopulation.TryUnassign을 거치지 않고
        // PopulationAllocation을 직접 줄이므로(PopulationManager.ReduceAssignedPopulation),
        // 인구 변화 자체를 구독해야 마커의 가동 표시가 굳지 않는다.
        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnChunkStateChanged.RemoveListener(RefreshConquestState);
        }

        if (_populationManager != null)
        {
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);
        }
    }

    private void HandlePopulationChanged(PopulationState _) => RefreshMarkers();

    private void Start()
    {
        // 생성은 Start에서 동기로 끝낸다. 한 프레임 미루면 안 되는 이유:
        // SaveService.RunLoadFlowAsync도 Start에서 UniTask.Yield() 하나만 두고 복원을 시작하는데,
        // Start끼리는 순서가 보장되지 않으므로 그쪽 연속이 먼저 깨어나면 _landmarks가 아직 비어 있어
        // RestoreOperation이 아무 것도 못 찾고 조용히 넘어간다(불러오기 후 전 랜드마크 인구 0).
        // 생성 자체는 GridMap.Awake가 만든 청크만 있으면 되므로 미룰 이유가 없다.
        SpawnLandmarks();
        RefreshConquestStateAfterAllStartsAsync().Forget();
    }

    // 초기 점령 상태 반영만 한 프레임 미룬다 - 성의 홈 청크 점령은 Castle.Start가 하고,
    // Start끼리는 순서가 보장되지 않기 때문이다(CLAUDE.md 이벤트 초기화 규칙).
    private async UniTaskVoid RefreshConquestStateAfterAllStartsAsync()
    {
        await UniTask.Yield(this.GetCancellationTokenOnDestroy());

        _isReady = true;
        RefreshConquestState();
    }

    private void SpawnLandmarks()
    {
        if (_gridMap == null || _landmarkTable == null)
        {
            Debug.LogError(
                "[LandmarkManager] GridMap 또는 ChunkLandmarkTable 참조가 없어 랜드마크가 하나도 생성되지 않습니다.",
                this);
            return;
        }

        Transform root = _landmarkRoot != null ? _landmarkRoot : transform;

        foreach (KeyValuePair<Vector2Int, LandmarkDataSO> pair in _landmarkTable.LandmarksByCoord)
        {
            Landmark landmark = CreateLandmark(pair.Key, pair.Value, root);

            if (landmark != null)
            {
                _landmarks.Add(landmark);
            }
        }
    }

    private Landmark CreateLandmark(Vector2Int chunkCoord, LandmarkDataSO data, Transform root)
    {
        if (_gridMap.GetChunk(chunkCoord) == null)
        {
            Debug.LogWarning(
                $"[LandmarkManager] 랜드마크 '{data.name}'가 맵에 없는 청크 {chunkCoord}에 배치되어 있습니다.");
            return null;
        }

        var landmarkObject = new GameObject(data.name);
        landmarkObject.transform.SetParent(root, false);
        landmarkObject.transform.position = _gridMap.GetChunkCenterWorld(chunkCoord);

        Landmark landmark = landmarkObject.AddComponent<Landmark>();
        landmark.Initialize(data, chunkCoord);

        // 시각물은 순수 아트 프리팹이다 - Landmark 컴포넌트를 프리팹에 붙여둘 필요가 없다.
        if (data.WorldPrefab != null)
        {
            Instantiate(data.WorldPrefab, landmarkObject.transform);
        }

        if (data.IsOperable)
        {
            var population = landmarkObject.AddComponent<LandmarkPopulation>();

            // 초기화에 실패했으면 붙이지 않는다. 붙여 두면 Landmark.Population이 non-null이라
            // 툴팁이 "인구 0 / 0"을 정상처럼 그리고 배치는 전부 조용히 실패한다.
            if (population.Initialize(_populationManager, _cycleManager, this))
            {
                landmark.BindPopulation(population);
            }
            else
            {
                Debug.LogError(
                    $"[LandmarkManager] 랜드마크 '{data.name}'의 인구 할당 초기화에 실패해 " +
                    $"가동할 수 없습니다(PopulationManager·CycleManager 참조 확인 필요).", this);
                Destroy(population);
            }
        }

        CreateMarker(landmark, landmarkObject.transform);
        return landmark;
    }

    private void CreateMarker(Landmark landmark, Transform parent)
    {
        if (_markerPrefab == null)
        {
            return;
        }

        LandmarkMarker marker = Instantiate(_markerPrefab, parent);
        marker.transform.localPosition = new Vector3(0f, _markerYOffset, 0f);
        marker.Bind(landmark);
        landmark.BindMarker(marker);
    }

    // 마커를 별도 리스트가 아니라 랜드마크에서 되짚어 간다 - 소멸한 랜드마크의 마커가
    // 리스트에 남아 파괴된 오브젝트를 Refresh하는 사고를 구조적으로 막는다(Landmark.Marker 주석 참고).
    private void RefreshMarkers()
    {
        foreach (Landmark landmark in _landmarks)
        {
            if (landmark.Marker != null)
            {
                landmark.Marker.Refresh();
            }
        }
    }

    /// <summary>
    /// 청크 상태가 바뀔 때마다 랜드마크의 상태를 다시 반영하고, 새로 점령된 것을 수령한다.
    /// 세이브 복원도 이 경로를 한 번 거쳐야 한다(SaveRestore 참고) - 복원 전후 점령지가 같으면
    /// GridMap이 OnChunkStateChanged를 쏘지 않아 여기가 한 번도 돌지 않을 수 있다.
    /// </summary>
    public void RefreshConquestState()
    {
        if (!_isReady)
        {
            return;
        }

        foreach (Landmark landmark in _landmarks)
        {
            Chunk chunk = _gridMap.GetChunk(landmark.ChunkCoord);
            landmark.SetState(chunk != null ? chunk.CurrentState : ChunkState.Hidden);

            if (landmark.IsConquered)
            {
                TryClaim(landmark);
            }
        }

        DespawnClaimedLandmarks();
        RefreshMarkers();
    }

    // 소멸 조건을 "점령했을 때"가 아니라 "수령을 마쳤을 때"로 잡으면 실시간 점령과 세이브 복원이
    // 한 경로로 처리된다. 복원은 RestoreClaims로 수령 이력만 되살리고 TryClaim을 타지 않으므로,
    // 점령 시점에 지우는 방식으로 짜면 불러오기 후 이미 받은 둥지가 되살아난다.
    private void DespawnClaimedLandmarks()
    {
        for (int i = _landmarks.Count - 1; i >= 0; i--)
        {
            Landmark landmark = _landmarks[i];

            if (landmark.Data == null ||
                !landmark.Data.DespawnsOnClaim ||
                !IsClaimed(landmark.Data))
            {
                continue;
            }

            _landmarks.RemoveAt(i);
            Destroy(landmark.gameObject);
        }
    }

    /// <summary>
    /// 랜드마크 보상 수령의 단일 경로. 중복 수령 차단도 여기서만 한다.
    /// "점령 즉시"가 아니라 "별도 조사 행동 후"로 규칙을 바꾸고 싶어지면 이 메서드의
    /// 호출 시점만 옮기면 되고, 보상 SO들은 건드릴 필요가 없다.
    /// </summary>
    public bool TryClaim(Landmark landmark)
    {
        if (landmark == null || landmark.Data == null)
        {
            return false;
        }

        string landmarkId = landmark.Data.LandmarkId;

        if (string.IsNullOrWhiteSpace(landmarkId))
        {
            Debug.LogError(
                $"[LandmarkManager] 랜드마크 '{landmark.Data.name}'에 LandmarkId가 비어 있어 수령을 기록할 수 없습니다.");
            return false;
        }

        if (_claimedLandmarkIds.Contains(landmarkId))
        {
            return false;
        }

        // 지급을 먼저 하고, 전부 성공했을 때만 수령으로 기록한다.
        // 먼저 기록하면 참조 누락 등으로 지급이 실패해도 수령 처리가 되고, 그 이력이 세이브에
        // 그대로 남아 다시 불러와도 영영 못 받는다.
        // 실패로 기록하지 않으면 다음 청크 상태 변화 때 다시 시도된다 - 보상이 여러 개인데 일부만
        // 성공한 경우 성공분이 재지급될 수 있으나, 조용히 유실되는 편보다 낫다고 보고 이쪽을 택했다.
        if (!GrantRewards(landmark.Data))
        {
            return false;
        }

        _claimedLandmarkIds.Add(landmarkId);
        OnLandmarkClaimed?.Invoke(landmark.Data);
        return true;
    }

    // 보상이 하나도 없으면(예: 가동 전용 랜드마크) 성공으로 본다 - 지급할 것이 없는 것과
    // 지급에 실패한 것은 다르다.
    private bool GrantRewards(LandmarkDataSO data)
    {
        var context = new LandmarkGrantContext(data, _eggInventory, _resourceManager);
        bool isAllGranted = true;

        foreach (LandmarkRewardSO reward in data.ConquestRewards)
        {
            if (reward == null)
            {
                continue;
            }

            if (!reward.Grant(context))
            {
                Debug.LogError(
                    $"[LandmarkManager] 랜드마크 '{data.name}'의 보상 '{reward.name}' 지급에 실패해 " +
                    $"수령으로 기록하지 않습니다(참조 누락 확인 필요).", this);
                isAllGranted = false;
            }
        }

        return isAllGranted;
    }

    // --- ILandmarkOwnershipQuery ---

    public bool IsClaimed(string landmarkId)
    {
        return !string.IsNullOrWhiteSpace(landmarkId) && _claimedLandmarkIds.Contains(landmarkId);
    }

    public bool IsClaimed(LandmarkDataSO data)
    {
        return data != null && IsClaimed(data.LandmarkId);
    }

    /// <summary>
    /// 이 청크에 있는 랜드마크. 점령 여부와 무관하게 찾는다 - 점령 패널·청크 카드는
    /// "점령하면 무엇을 얻는지"를 미리 보여줘야 하므로 미점령 랜드마크도 조회할 수 있어야 한다.
    /// </summary>
    public bool TryGetLandmarkAt(Vector2Int chunkCoord, out Landmark landmark)
    {
        foreach (Landmark candidate in _landmarks)
        {
            if (candidate.ChunkCoord == chunkCoord)
            {
                landmark = candidate;
                return true;
            }
        }

        landmark = null;
        return false;
    }

    // --- 세이브 ---

    public IReadOnlyCollection<string> ClaimedLandmarkIds => _claimedLandmarkIds;

    /// <summary>
    /// 세이브 복원 전용. 보상 지급 경로(TryClaim)를 타지 않으므로 알·자원이 다시 지급되지 않는다.
    /// ConquestManager.RestoreTerritory보다 반드시 먼저 호출해야 한다 - 영토 복원이
    /// OnChunkStateChanged를 발화시켜 RefreshConquestState가 돌기 때문이다.
    /// </summary>
    public void RestoreClaims(IReadOnlyList<string> claimedLandmarkIds)
    {
        _claimedLandmarkIds.Clear();

        if (claimedLandmarkIds == null)
        {
            return;
        }

        foreach (string landmarkId in claimedLandmarkIds)
        {
            if (!string.IsNullOrWhiteSpace(landmarkId))
            {
                _claimedLandmarkIds.Add(landmarkId);
            }
        }
    }

    /// <summary>
    /// 세이브 복원 전용. 모든 랜드마크의 배치 인구를 비운다.
    /// 저장 당시 인구가 0이던 랜드마크는 Operations 목록에 아예 없으므로, 복원 직전에 한 번
    /// 비워 두지 않으면 그 랜드마크의 현재 배치가 그대로 남는다.
    /// </summary>
    public void ClearAllOperations()
    {
        foreach (Landmark landmark in _landmarks)
        {
            if (landmark.Population != null)
            {
                PopulationAssignmentRules.TryUnassignClamped(
                    landmark.Population,
                    landmark.Population.AssignedPopulation);
            }
        }
    }

    /// <summary>
    /// 세이브 복원 전용. 랜드마크별 배치 인구를 저장값으로 "갈아 끼운다".
    /// 더하지 않고 먼저 전부 회수하는 이유: 씬을 다시 로드하지 않고 진행 중인 게임에
    /// 불러오기를 하면 이미 배치된 인원 위에 저장값이 얹혀 정원까지 부풀어 오른다.
    /// (RestoreMaxPopulation 등 다른 복원 경로와 같은 "대입" 의미를 맞춘다.)
    /// </summary>
    public void RestoreOperation(string landmarkId, int assignedPopulation)
    {
        foreach (Landmark landmark in _landmarks)
        {
            if (landmark.Data == null ||
                landmark.Data.LandmarkId != landmarkId ||
                landmark.Population == null)
            {
                continue;
            }

            PopulationAssignmentRules.TryUnassignClamped(
                landmark.Population,
                landmark.Population.AssignedPopulation);

            if (assignedPopulation > 0)
            {
                PopulationAssignmentRules.TryAssignClamped(
                    landmark.Population,
                    _populationManager,
                    assignedPopulation);
            }

            return;
        }
    }

    /// <summary>인구 배치가 바뀐 뒤 호출해 가동 표시를 갱신한다.</summary>
    public void NotifyOperationChanged()
    {
        RefreshMarkers();
        OnLandmarkOperationChanged?.Invoke();
    }
}
