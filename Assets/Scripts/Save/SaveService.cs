using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 세이브 슬롯의 단일 소유자. 2일차부터 낮이 시작될 때마다 자동저장하고, UI에 슬롯 목록·저장·로드·삭제를
/// 제공한다.
/// (싱글톤 아님 - SettingsService와 같이 [SerializeField] 주입, 항상 활성인 오브젝트에 둔다.)
///
/// 계약 1: 자동저장 실패는 절대 게임 진행을 막지 않는다. 모든 예외는 결과값으로 변환된다.
/// 계약 2: 스냅샷은 "낮 시작 정산이 끝난 뒤" 임의 시점에 찍는다 - 자동저장은 그 경계
///         (CycleManager.OnDaySettled)에서, 수동 저장은 낮 아무 때나(CanSave)다. 복원은 정산을
///         재생하지 않고 CycleManager.ResumeDay()로 그 낮을 이어서 시작하므로, 낮에 저장→로드를
///         반복해도 생산·유지비·알 성장·성 회복·이동권이 다시 적용되지 않는다.
/// 계약 2-1: 1일차 시작은 자동저장하지 않는다. 게임오버 후 Restart는 씬 재로드로 새 런의 1일차를
///           시작하므로, 여기서 저장하면 직전 런의 이어하기가 통째로 날아간다(HandleDaySettled).
/// 계약 3: 로드는 항상 씬 재로드를 거친다. 살아 있는 몬스터·투사체·건물 GameObject를 정리할 방법이
///         없어 인게임 in-place 로드는 지원하지 않는다.
///
/// OnApplicationQuit 저장은 두지 않는다(SettingsService와 다른 선택). 그 콜백에서는 await가
/// 성립하지 않아 동기 폴백 경로가 하나 더 생기고, 낮마다 저장하므로 한계 가치가 낮으며,
/// 밤 중 종료라면 애초에 저장할 수 없는 상태(웨이브 진행은 저장 대상이 아님)라 반쪽 세이브가 된다.
/// </summary>
public sealed class SaveService : MonoBehaviour
{
    public const int MAX_SLOT_COUNT = SavePaths.MAX_SLOT_COUNT;
    public const int AUTO_SAVE_SLOT_INDEX = SavePaths.AUTO_SAVE_SLOT_INDEX;
    public const int INVALID_SLOT_INDEX = SavePaths.INVALID_SLOT_INDEX;

    // 연속 실패가 이 횟수에 이르면 자동저장을 끈다. 디스크가 가득 찼거나 권한이 없을 때
    // 낮마다 같은 예외 로그가 쌓이는 것을 막는다.
    private const int AUTO_SAVE_FAILURE_LIMIT = 3;

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private ConquestManager _conquestManager;
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private WaveCycleProgression _waveCycleProgression;
    [SerializeField] private Castle _castle;

    [Tooltip("랜드마크 수령 이력과 배치 인구를 저장한다. 랜드마크가 없는 씬에서는 비워 둔다.")]
    [SerializeField] private LandmarkManager _landmarkManager;

    [Tooltip("건물 배치 복원이 쓰는 id -> 프리팹 레지스트리. 비워 두면 건물이 복원되지 않는다.")]
    [SerializeField] private BuildingCatalog _buildingCatalog;

    [Tooltip("복원된 새끼용 타워를 보유 레코드에 다시 결속한다. 새끼용이 없는 씬에서는 비워 둔다.")]
    [SerializeField] private BabyDragonPlacementCoordinator _babyDragonPlacementCoordinator;

    [Tooltip("슬롯 목록에 띄울 점령 현황 썸네일을 찍는다. 비워 두면 썸네일 없이 저장한다.")]
    [SerializeField] private SaveThumbnailCapturer _thumbnailCapturer;

    [Tooltip("2일차부터 낮이 시작될 때마다 자동으로 저장할지 여부. 1일차는 항상 저장하지 않는다.")]
    [SerializeField] private bool _isAutoSaveEnabled = true;

#if UNITY_EDITOR
    [Header("[테스트 전용] 플레이 진입 시 강제 이어하기")]
    [SerializeField] private bool _debugForceContinue;
    [SerializeField] private int _debugSlotIndex = SavePaths.AUTO_SAVE_SLOT_INDEX;
#endif

    [SerializeField] private UnityEvent<SaveSlotInfo> _saveCompleted = new();
    [SerializeField] private UnityEvent<SaveResult> _saveFailed = new();

    // 복원 후 1회 발화. UI가 이걸 구독해 전체를 다시 그리면 개별 이벤트 누락에도 견딘다.
    [SerializeField] private UnityEvent _loadCompleted = new();

    private int _pendingLoadSlotIndex = SavePaths.INVALID_SLOT_INDEX;
    private int _consecutiveAutoSaveFailures;
    private bool _isSaving;

    // 복원 중에는 저장하지 않는다. ResumeDay는 OnDaySettled를 발화하지 않으므로 이 플래그가
    // 없어도 자동저장이 돌지 않지만, 복원 도중 다른 경로로 저장이 시작되는 것을 막는 이중 방어로 남긴다.
    private bool _isRestoring;

    public UnityEvent<SaveSlotInfo> SaveCompleted => _saveCompleted;
    public UnityEvent<SaveResult> SaveFailed => _saveFailed;
    public UnityEvent LoadCompleted => _loadCompleted;

    public bool HasPendingLoad => _pendingLoadSlotIndex != SavePaths.INVALID_SLOT_INDEX;

    private void Awake()
    {
        // GameManager.Start가 이 값을 보고 새 게임/이어하기를 가른다.
        // Awake에서 소비하므로 Awake끼리의 순서와 무관하게 모든 Start보다 앞선다.
        if (SaveLoadRequest.TryConsume(out int slotIndex))
        {
            _pendingLoadSlotIndex = slotIndex;
            return;
        }

#if UNITY_EDITOR
        if (_debugForceContinue && SavePaths.IsValidSlotIndex(_debugSlotIndex))
        {
            _pendingLoadSlotIndex = _debugSlotIndex;
        }
#endif
    }

    // 구독은 OnEnable에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDaySettled.AddListener(HandleDaySettled);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDaySettled.RemoveListener(HandleDaySettled);
        }
    }

    // --- 조회 (SaveSlotQuery로 위임. 씬 없이도 읽혀야 해서 로직은 static 쪽에 있다) ---

    public IReadOnlyList<SaveSlotInfo> GetSlots() => SaveSlotQuery.GetSlots();

    public bool TryGetSlot(int slotIndex, out SaveSlotInfo info) =>
        SaveSlotQuery.TryGetSlot(slotIndex, out info);

    public bool HasAnySave => SaveSlotQuery.HasAnySave;

    public int MostRecentSlotIndex => SaveSlotQuery.MostRecentSlotIndex;

    /// <summary>
    /// 슬롯의 점령 현황 썸네일을 UI가 바로 붙일 수 있는 스프라이트로 읽는다.
    /// 부를 때마다 텍스처를 새로 만들므로, 슬롯을 다시 그릴 때는 호출자가 이전 스프라이트와
    /// 그 스프라이트의 texture를 함께 Destroy해야 한다.
    /// </summary>
    public bool TryLoadThumbnail(int slotIndex, out Sprite thumbnail) =>
        SaveSlotQuery.TryLoadThumbnail(slotIndex, out thumbnail);

    // --- 저장 ---

    /// <summary>
    /// 지금 국면이 저장을 허용하는지. 진행 중인 저장(_isSaving)은 보지 않는다 -
    /// "저장할 수 없는 상태"와 "지금 저장이 돌고 있다"는 UI에 다르게 비쳐야 한다.
    /// 저장 버튼처럼 창을 열 때 한 번만 판정하는 곳은 이 값을 봐야 한다. CanSave를 보면
    /// 낮이 시작될 때 도는 자동저장의 디스크 쓰기에 걸려, 저장할 수 있는 낮인데도
    /// 버튼이 잠긴 채 창을 다시 열 때까지 풀리지 않는다.
    /// </summary>
    public bool IsSaveablePhase =>
        !_isRestoring &&
        _gameManager != null &&
        !_gameManager.IsGameEnded &&
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Day;

    public bool CanSave => IsSaveablePhase && !_isSaving;

    public async UniTask<SaveResult> SaveAsync(
        int slotIndex,
        bool isAutoSave = false,
        CancellationToken cancellationToken = default)
    {
        if (!SavePaths.IsValidSlotIndex(slotIndex))
        {
            return Fail(SaveFailureReason.InvalidSlot, slotIndex);
        }

        if (_isSaving)
        {
            return Fail(SaveFailureReason.AlreadyRunning, slotIndex);
        }

        if (!CanSave)
        {
            return Fail(SaveFailureReason.NotSaveablePhase, slotIndex);
        }

        var context = BuildContext();
        if (!context.IsValid)
        {
            return Fail(SaveFailureReason.CaptureFailed, slotIndex);
        }

        _isSaving = true;

        try
        {
            // 캡처와 직렬화는 메인 스레드에서 동기로 한다 - GridMap 등 Unity API를 만지고,
            // 저장 시점의 상태를 한 프레임 안에서 원자적으로 고정해야 한다.
            SaveGameDto dto = SaveCapture.Capture(context, slotIndex, isAutoSave);

            if (!SaveJson.TrySerialize(dto, out string saveJson, out string serializeError) ||
                !SaveJson.TrySerialize(dto.Meta, out string metaJson, out serializeError))
            {
                Debug.LogError($"[SaveService] 직렬화 실패: {serializeError}");
                return Fail(SaveFailureReason.SerializationFailed, slotIndex);
            }

            // 썸네일도 캡처와 같은 이유로 메인 스레드에서 찍는다(카메라·텍스처를 만진다).
            // 실패는 무시한다 - 메타 파일과 같은 파생물이라 없어도 세이브는 온전하다.
            byte[] thumbnailPng = null;
            _thumbnailCapturer?.TryCapturePng(out thumbnailPng);

            // 경로는 반드시 메인 스레드에서 만든다 - SavePaths.RootDirectory가
            // Application.persistentDataPath를 읽는데, 이건 메인 스레드 전용 Unity API다.
            string savePath = SavePaths.SaveFilePath(slotIndex);
            string metaPath = SavePaths.MetaFilePath(slotIndex);
            string thumbnailPath = SavePaths.ThumbnailFilePath(slotIndex);

            // 디스크 쓰기만 스레드풀로 뺀다. 페이로드는 수 KB지만 백신 실시간 검사나 동기화 폴더가
            // 걸리면 수십~수백 ms 블록될 수 있고, 이 저장은 낮 전환 프레임에 걸려 있다.
            string writeError;

            try
            {
                writeError = await UniTask.RunOnThreadPool(
                    () => WriteSlotFiles(savePath, metaPath, thumbnailPath, saveJson, metaJson, thumbnailPng),
                    cancellationToken: cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                throw;
            }
            catch (System.Exception exception)
            {
                // 계약 1(자동저장 실패가 게임을 멈추지 않는다)을 지키려면 여기서 새는 예외가 없어야 한다.
                Debug.LogException(exception);
                return Fail(SaveFailureReason.DiskWriteFailed, slotIndex);
            }

            if (writeError != null)
            {
                Debug.LogError($"[SaveService] 세이브 쓰기 실패(슬롯 {slotIndex}): {writeError}");
                return Fail(SaveFailureReason.DiskWriteFailed, slotIndex);
            }

            _consecutiveAutoSaveFailures = 0;

            SaveSlotInfo info = SaveSlotInfo.FromMeta(dto.Meta, slotIndex, false, thumbnailPng != null);
            _saveCompleted.Invoke(info);
            return SaveResult.Success(info);
        }
        finally
        {
            _isSaving = false;
        }
    }

    public bool TryDelete(int slotIndex) => SaveSlotQuery.TryDelete(slotIndex);

    // --- 로드 ---

    /// <summary>슬롯을 예약하고 현재 씬을 다시 로드한다. 복원은 GameManager.Start에서 이어진다.</summary>
    public void RequestLoadAndReloadScene(int slotIndex)
    {
        if (!SavePaths.IsValidSlotIndex(slotIndex))
        {
            Debug.LogError($"[SaveService] 잘못된 슬롯 인덱스: {slotIndex}");
            return;
        }

        SaveLoadRequest.Request(slotIndex);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// GameManager.Start가 대기 중인 로드 요청을 발견했을 때 호출한다.
    /// 복원이 끝나면 이 클래스가 ResumeDay까지 책임지고, 실패하면 새 게임으로 폴백한다 -
    /// 어느 쪽이든 게임이 시작되지 않는 상태로 남지 않는다.
    /// </summary>
    public void BeginLoadFlow(GameManager gameManager)
    {
        RunLoadFlowAsync(gameManager, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid RunLoadFlowAsync(GameManager gameManager, CancellationToken cancellationToken)
    {
        int slotIndex = _pendingLoadSlotIndex;
        _pendingLoadSlotIndex = SavePaths.INVALID_SLOT_INDEX;

        // 다른 오브젝트의 Start가 모두 끝난 뒤여야 한다 - 특히 Castle.Start가 홈 청크를 점령
        // 상태로 만들고 성 체력을 초기화한다. Start끼리는 순서가 보장되지 않으므로 한 프레임 미룬다
        // (CLAUDE.md 이벤트 초기화 규칙, FogOfWarRenderer와 같은 타이밍).
        await UniTask.Yield(cancellationToken);

        SaveLoadResult result;

        // 복원 도중 예외가 나면 자원·연구가 반쯤 적용된 상태다. 그대로 StartNewRun을 태우면
        // 그 잔재가 새 런에 섞이므로, 씬을 클린 리로드해 메모리 상태를 버린다.
        // SaveLoadRequest는 이미 소비돼 있어 리로드된 씬은 HasPendingLoad == false가 되고,
        // GameManager.Start가 StartNewRun으로 간다 - 리로드는 1회뿐이라 루프가 생기지 않는다.
        try
        {
            result = TryApplySlot(slotIndex);
        }
        catch (System.OperationCanceledException)
        {
            throw;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError($"[SaveService] 슬롯 {slotIndex} 복원 중 예외 - 씬을 다시 로드해 새 게임으로 시작합니다.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // 읽기·검증 실패는 상태를 하나도 바꾸지 않았으므로 씬 리로드 없이 새 게임으로 간다.
        if (!result.IsSuccess)
        {
            Debug.LogError($"[SaveService] 슬롯 {slotIndex} 로드 실패({result.Reason}) - 새 게임으로 시작합니다.");
            gameManager.StartNewRun();
            return;
        }

        // 복원 성공 시 1회, ResumeDay 이후에 발화한다(UI가 확정된 낮 상태를 보게 하려면 이 순서여야 한다).
        _loadCompleted.Invoke();
    }

    private SaveLoadResult TryApplySlot(int slotIndex)
    {
        if (!SaveSlotQuery.TryReadSave(slotIndex, out SaveGameDto dto, out SaveLoadFailureReason reason))
        {
            return SaveLoadResult.Failure(reason);
        }

        var context = BuildContext();
        if (!context.IsValid)
        {
            return SaveLoadResult.Failure(SaveLoadFailureReason.ValidationFailed);
        }

        _isRestoring = true;

        try
        {
            SaveRestore.Apply(dto, context);

            // 복원된 일차를 이어서 시작한다. 조명·UI·웨이브 스냅샷·포탈 개방이 평소 경로로
            // 갱신되지만 정산은 재생되지 않는다 - 스냅샷이 이미 정산 이후 상태다(계약 2).
            _cycleManager.ResumeDay();
        }
        finally
        {
            _isRestoring = false;
        }

        return SaveLoadResult.Success();
    }

    // --- 내부 헬퍼 ---

    private void HandleDaySettled(int dayNumber)
    {
        if (!_isAutoSaveEnabled || _isRestoring)
        {
            return;
        }

        // 1일차 시작은 저장하지 않는다. 게임오버 후 Restart(UI_GameOverWindow)는 씬을 다시 로드해
        // 새 런의 1일차를 시작하는데, 여기서 저장하면 직전 런의 이어하기(slot_00)가 아직 아무것도
        // 진행하지 않은 상태로 덮어써진다. 새 런은 2일차가 시작될 때 처음 기록된다.
        if (dayNumber <= SaveValidation.FIRST_DAY_NUMBER)
        {
            return;
        }

        AutoSaveAsync(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid AutoSaveAsync(CancellationToken cancellationToken)
    {
        SaveResult result = await SaveAsync(AUTO_SAVE_SLOT_INDEX, true, cancellationToken);

        if (result.IsSuccess)
        {
            return;
        }

        _saveFailed.Invoke(result);
        _consecutiveAutoSaveFailures++;

        if (_consecutiveAutoSaveFailures >= AUTO_SAVE_FAILURE_LIMIT)
        {
            _isAutoSaveEnabled = false;
            Debug.LogError(
                $"[SaveService] 자동저장이 {AUTO_SAVE_FAILURE_LIMIT}회 연속 실패해 비활성화합니다. 마지막 사유: {result.Reason}");
        }
    }

    // 스레드풀에서 실행된다. Unity API를 절대 만지면 안 되므로 경로를 완성된 문자열로,
    // 썸네일은 이미 인코딩된 바이트로 받는다
    // (SavePaths는 Application.persistentDataPath를 읽어 메인 스레드 전용이다).
    // 본문을 먼저 확정한 뒤 메타를 쓴다 - 반대 순서면 "새 메타 + 옛 본문" 상태가 잠깐 생긴다.
    private static string WriteSlotFiles(
        string savePath,
        string metaPath,
        string thumbnailPath,
        string saveJson,
        string metaJson,
        byte[] thumbnailPng)
    {
        if (!SaveFileStore.TryWriteAtomic(savePath, saveJson, out string error))
        {
            return error;
        }

        // 메타는 파생 캐시이므로 실패해도 저장 자체는 성공으로 본다.
        // 다음 슬롯 조회 때 본문에서 다시 만들어진다(TryGetSlot 참고).
        SaveFileStore.TryWriteAtomic(metaPath, metaJson, out _);

        // 썸네일도 같은 이유로 실패를 무시한다. 다만 본문에서 되살릴 수 없으므로,
        // 이 슬롯은 다음에 저장할 때까지 썸네일 없이 표시된다.
        if (thumbnailPng != null)
        {
            SaveFileStore.TryWriteBytesAtomic(thumbnailPath, thumbnailPng, out _);
        }

        return null;
    }

    private SaveResult Fail(SaveFailureReason reason, int slotIndex) =>
        SaveResult.Failure(reason, slotIndex);

    private SaveCaptureContext BuildContext() => new SaveCaptureContext(
        _gameManager,
        _cycleManager,
        _resourceManager,
        _populationManager,
        _researchManager,
        _dragonTreeManager,
        _conquestManager,
        _gridMap,
        _waveCycleProgression,
        _castle,
        _landmarkManager,
        _buildingCatalog,
        _babyDragonPlacementCoordinator);
}
