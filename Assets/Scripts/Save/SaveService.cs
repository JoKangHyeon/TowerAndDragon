using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
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

    // 썸네일 캡처(맵 전체 재렌더 + GPU 리드백 + PNG 인코딩)를 본문 저장 프레임에서 몇 프레임
    // 뒤로 미룰지. 밤→낮 전환처럼 이미 무거운 프레임과 겹치지 않을 정도면 충분하다.
    private const int THUMBNAIL_CAPTURE_DELAY_FRAMES = 3;

    // [임시 계측] 밤→낮 전환 프리즈 조사용. CycleManager.cs의 TND. 명명 규칙을 그대로 따른다.
    private const string SAVE_CAPTURE_MARKER_NAME = "TND.Save.Capture";
    private const string SAVE_SERIALIZE_MARKER_NAME = "TND.Save.Serialize";
    private const string SAVE_THUMBNAIL_MARKER_NAME = "TND.Save.Thumbnail";

    private static readonly ProfilerMarker SAVE_CAPTURE_MARKER = new(SAVE_CAPTURE_MARKER_NAME);
    private static readonly ProfilerMarker SAVE_SERIALIZE_MARKER = new(SAVE_SERIALIZE_MARKER_NAME);
    private static readonly ProfilerMarker SAVE_THUMBNAIL_MARKER = new(SAVE_THUMBNAIL_MARKER_NAME);

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

    [Tooltip("새 게임 +(뮤테이터) 서비스. 비워 두면 뮤테이터 없는 표준 모드로 동작한다"
        + " - 튜토리얼·테스트 씬에는 두지 않는다.")]
    [SerializeField]
    [WiringOptional]
    private RunModifierService _runModifierService;

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

    // 철인 모드에서 세이브를 지운 직후 1회 발화. 조용히 사라지면 버그로 읽힌다.
    [SerializeField] private UnityEvent _ironmanSaveDeleted = new();

    private int _pendingLoadSlotIndex = SavePaths.INVALID_SLOT_INDEX;
    private int _consecutiveAutoSaveFailures;
    private bool _isSaving;

    // 지연된 썸네일 캡처가 겹치지 않게 막는 별도 가드. _isSaving과 분리한 이유: 썸네일 캡처는
    // 본문 저장이 끝난 뒤(디스크 쓰기 스레드가 아니라 메인 스레드가 자유로워진 뒤) 몇 프레임 지나
    // 진행되므로, 그동안 _isSaving을 계속 잡고 있으면 그 사이의 수동 저장이 AlreadyRunning으로
    // 거부된다.
    private bool _isThumbnailCapturing;

    // 복원 중에는 저장하지 않는다. ResumeDay는 OnDaySettled를 발화하지 않으므로 이 플래그가
    // 없어도 자동저장이 돌지 않지만, 복원 도중 다른 경로로 저장이 시작되는 것을 막는 이중 방어로 남긴다.
    private bool _isRestoring;

    // 철인 모드에서 이 런이 묶인 슬롯. RunData나 세이브 DTO가 아니라 여기 두는 이유:
    // 이것은 런의 시뮬레이션 상태가 아니라 "이 세션이 어떤 파일을 쓰는가"이고, 그 값은 이미
    // 로드 요청(_pendingLoadSlotIndex)이 들고 있다. DTO에 넣으면 SaveSlotInfo.FromMeta 주석이
    // 경고하는 함정과 같아진다 - 세이브 폴더를 다른 슬롯으로 복사하면 파일 안의 번호와 실제 폴더가
    // 어긋나 잠금이 엉뚱한 슬롯을 가리킨다.
    private int _ironmanSlotIndex = SavePaths.INVALID_SLOT_INDEX;

    public UnityEvent<SaveSlotInfo> SaveCompleted => _saveCompleted;
    public UnityEvent<SaveResult> SaveFailed => _saveFailed;
    public UnityEvent LoadCompleted => _loadCompleted;
    public UnityEvent IronmanSaveDeleted => _ironmanSaveDeleted;

    /// <summary>철인 모드 게임오버로 세이브를 지웠는가.
    /// 게임오버 창은 비활성으로 저장돼 있어 삭제 시점에 OnEnable이 아직 돌지 않는다 -
    /// 이벤트만으로는 놓치므로, 늦게 열리는 구독자가 현재 값을 한 번 읽을 수 있게 플래그도 남긴다
    /// (CLAUDE.md "구독 직후 현재 값을 한 번 수동 반영" 규칙).</summary>
    public bool WasIronmanSaveDeleted { get; private set; }

    public bool HasPendingLoad => _pendingLoadSlotIndex != SavePaths.INVALID_SLOT_INDEX;

    /// <summary>철인 모드에서 이 런이 고정된 슬롯. 철인이 아니거나 아직 고정되지 않았으면
    /// <see cref="INVALID_SLOT_INDEX"/>다. 슬롯 목록 UI가 자동저장 슬롯 배제 규칙의 예외를
    /// 판정하는 데 쓴다 - 고정 슬롯이 자동저장 슬롯일 수 있기 때문이다.</summary>
    public int IronmanSlotIndex => IsIronman ? _ironmanSlotIndex : SavePaths.INVALID_SLOT_INDEX;

    /// <summary>철인 모드(ironman)가 켜져 있는가. 서비스가 없으면 항상 false라 기존 동작과 같다.</summary>
    private bool IsIronman =>
        RunModifiers.SnapshotOf(_runModifierService).HasRule(RunRuleFlag.Ironman);

    /// <summary>철인 모드에서 자동저장·삭제가 향하는 슬롯.
    /// 아직 고정되지 않았으면 자동저장 슬롯이다 - 새 런의 첫 기록은 낮 정산 자동저장이므로,
    /// 수동 저장을 한 번도 하지 않은 런은 이어하기 슬롯 하나만 쓴다.</summary>
    private int EffectiveIronmanSlotIndex =>
        _ironmanSlotIndex != SavePaths.INVALID_SLOT_INDEX ? _ironmanSlotIndex : AUTO_SAVE_SLOT_INDEX;

    private void Awake()
    {
        // GameManager.Start가 이 값을 보고 새 게임/이어하기를 가른다.
        // Awake에서 소비하므로 Awake끼리의 순서와 무관하게 모든 Start보다 앞선다.
        if (SaveLoadRequest.TryConsume(out int slotIndex))
        {
            _pendingLoadSlotIndex = slotIndex;

            // 불러오기로 이어간 런은 같은 파일을 계속 쓴다 - 철인 모드에서 슬롯을 갈아타
            // 잠금을 우회하는 길을 막는다. 철인이 아니면 이 값은 아무 곳에서도 읽히지 않는다.
            _ironmanSlotIndex = slotIndex;
            return;
        }

#if UNITY_EDITOR
        if (_debugForceContinue && SavePaths.IsValidSlotIndex(_debugSlotIndex))
        {
            _pendingLoadSlotIndex = _debugSlotIndex;
            _ironmanSlotIndex = _debugSlotIndex;
        }
#endif
    }

    // 구독은 OnEnable에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDaySettled.AddListener(HandleDaySettled);

            // OnDayEnd는 OnDaySettled와 달리 인라인 초기화가 없어 씬 YAML에 항목이 없으면 null이다
            // (OnDayStart·OnNightStart와 같은 사정). SoundManager의 ?.AddListener 관용구를 따른다.
            _cycleManager.OnDayEnd?.AddListener(HandleDayEnd);
        }

        if (_gameManager != null)
        {
            // 승리(VictoryOccurred)에는 구독하지 않는다 - 철인 모드는 패배만 되돌릴 수 없게 만든다.
            _gameManager.GameOverOccurred?.AddListener(HandleGameOver);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDaySettled.RemoveListener(HandleDaySettled);
            _cycleManager.OnDayEnd?.RemoveListener(HandleDayEnd);
        }

        if (_gameManager != null)
        {
            _gameManager.GameOverOccurred?.RemoveListener(HandleGameOver);
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

    /// <summary>
    /// 이 슬롯에 지금 저장할 수 있는지. <see cref="CanSave"/>는 슬롯을 모르는 판정이라 그대로 두고
    /// (창을 열 때 한 번 보는 IsSaveablePhase와의 역할 구분을 깨지 않기 위해) 슬롯 조건만 여기 얹는다.
    /// 슬롯 목록 UI가 줄마다 이 값으로 선택 가능 여부를 가른다.
    /// </summary>
    public bool CanSaveToSlot(int slotIndex) => CanSave && IsSlotAllowedForSave(slotIndex);

    /// <summary>철인 모드에서 고정 슬롯 외 저장을 막는다. 철인이 아니면 항상 참이다.
    /// 아직 고정되지 않았다면(수동 저장을 한 번도 안 한 런) 어느 슬롯이든 허용하고,
    /// 그 첫 저장이 슬롯을 고정한다.</summary>
    private bool IsSlotAllowedForSave(int slotIndex) =>
        !IsIronman ||
        _ironmanSlotIndex == SavePaths.INVALID_SLOT_INDEX ||
        _ironmanSlotIndex == slotIndex;

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

        // 국면 검사 뒤에 둔다 - 밤에 잠긴 슬롯을 눌렀을 때 "낮에만 저장할 수 있습니다"가 먼저 보여야
        // 한다(철인 잠금은 낮이 되면 풀리는 것이 아니라 그 슬롯이 아니라는 뜻이므로 더 좁은 사유다).
        if (!IsSlotAllowedForSave(slotIndex))
        {
            return Fail(SaveFailureReason.IronmanSlotLocked, slotIndex);
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
            SaveGameDto dto;
            using (SAVE_CAPTURE_MARKER.Auto())
            {
                dto = SaveCapture.Capture(context, slotIndex, isAutoSave);
            }

            string saveJson;
            string metaJson;
            using (SAVE_SERIALIZE_MARKER.Auto())
            {
                if (!SaveJson.TrySerialize(dto, out saveJson, out string serializeError) ||
                    !SaveJson.TrySerialize(dto.Meta, out metaJson, out serializeError))
                {
                    Debug.LogError($"[SaveService] 직렬화 실패: {serializeError}");
                    return Fail(SaveFailureReason.SerializationFailed, slotIndex);
                }
            }

            // 경로는 반드시 메인 스레드에서 만든다 - SavePaths.RootDirectory가
            // Application.persistentDataPath를 읽는데, 이건 메인 스레드 전용 Unity API다.
            string savePath = SavePaths.SaveFilePath(slotIndex);
            string metaPath = SavePaths.MetaFilePath(slotIndex);
            string thumbnailPath = SavePaths.ThumbnailFilePath(slotIndex);

            // 디스크 쓰기만 스레드풀로 뺀다. 페이로드는 수 KB지만 백신 실시간 검사나 동기화 폴더가
            // 걸리면 수십~수백 ms 블록될 수 있고, 이 저장은 낮 전환 프레임에 걸려 있다.
            // 썸네일은 여기서 찍지 않는다 - 본문은 저장 시점 상태를 원자적으로 고정해야 하는 계약
            // 대상이지만, 썸네일은 실패해도 세이브가 온전한 파생물이라(WriteSlotFiles 주석 참고)
            // 이 프레임 밖으로 미뤄도 계약을 깨지 않는다(CaptureAndWriteThumbnailDelayedAsync).
            string writeError;

            try
            {
                writeError = await UniTask.RunOnThreadPool(
                    () => WriteSlotFiles(savePath, metaPath, thumbnailPath, saveJson, metaJson, null),
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

            // 철인 모드의 슬롯 고정은 첫 수동 저장이 정한다. 자동저장으로 고정하지 않는 이유:
            // 새 런의 첫 기록은 2일차 낮 정산 자동저장(0번)이라, 그것으로 고정하면 플레이어가
            // 고를 여지 없이 항상 이어하기 슬롯에 묶인다.
            if (!isAutoSave && IsIronman && _ironmanSlotIndex == SavePaths.INVALID_SLOT_INDEX)
            {
                _ironmanSlotIndex = slotIndex;
                Debug.Log($"[SaveService] 철인 모드 - 이 런의 세이브 슬롯을 {slotIndex}번으로 고정합니다.");
            }

            // 본문 저장이 끝난 뒤(아래 finally에서 _isSaving이 풀린 뒤) 별도로 썸네일을 캡처한다.
            // 지금 시점엔 아직 파일이 없으므로 hasThumbnail은 정직하게 false로 보고한다 - 슬롯 목록을
            // 다시 열면 SaveSlotQuery가 디스크에서 존재 여부를 새로 읽으므로 그때는 정확히 반영된다.
            CaptureAndWriteThumbnailDelayedAsync(
                thumbnailPath,
                _cycleManager != null ? _cycleManager.CurrentCycle : CycleManager.CycleState.Day,
                this.GetCancellationTokenOnDestroy()).Forget();

            SaveSlotInfo info = SaveSlotInfo.FromMeta(dto.Meta, slotIndex, false, false);
            _saveCompleted.Invoke(info);
            return SaveResult.Success(info);
        }
        finally
        {
            _isSaving = false;
        }
    }

    // 썸네일은 맵 전체를 다시 렌더 + GPU 리드백 + PNG 인코딩하는 무거운 파생물이다. 밤→낮 전환처럼
    // 이미 무거운 프레임과 겹치면 그 프레임만 눈에 띄게 멈추므로, 본문 저장 성공 뒤 몇 프레임
    // 지나 한가해진 시점에 캡처한다.
    private async UniTaskVoid CaptureAndWriteThumbnailDelayedAsync(
        string thumbnailPath,
        CycleManager.CycleState expectedCycle,
        CancellationToken cancellationToken)
    {
        // 같은 슬롯에 자동저장이 연달아 걸리는 극단적인 경우에도 지연 캡처끼리 겹치지 않게 한다.
        if (_isThumbnailCapturing)
            return;

        _isThumbnailCapturing = true;

        try
        {
            await UniTask.DelayFrame(THUMBNAIL_CAPTURE_DELAY_FRAMES, cancellationToken: cancellationToken);

            // 지연된 프레임 사이 낮/밤이 바뀌었으면(철인 모드의 밤 진입 직전 저장 등) 캡처를 건너뛴다 -
            // 그렇지 않으면 "낮 스냅샷을 저장했는데 밤 화면이 찍힌" 썸네일이 생긴다.
            if (_cycleManager != null && _cycleManager.CurrentCycle != expectedCycle)
                return;

            if (_thumbnailCapturer == null)
                return;

            byte[] thumbnailPng;
            using (SAVE_THUMBNAIL_MARKER.Auto())
            {
                if (!_thumbnailCapturer.TryCapturePng(out thumbnailPng))
                    return;
            }

            // 본문과 같은 이유로 디스크 쓰기만 스레드풀로 뺀다. 실패는 무시한다 - 메타 파일과 같은
            // 파생물이라 없어도 세이브는 온전하다(WriteSlotFiles 주석과 같은 계약).
            await UniTask.RunOnThreadPool(
                () => SaveFileStore.TryWriteBytesAtomic(thumbnailPath, thumbnailPng, out _),
                cancellationToken: cancellationToken);
        }
        catch (System.OperationCanceledException)
        {
            // 씬 언로드 등으로 취소된 경우 - 썸네일은 파생물이라 조용히 포기한다.
        }
        finally
        {
            _isThumbnailCapturing = false;
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

        if (!CanRestoreMutators(dto.Run.Mutators))
        {
            return SaveLoadResult.Failure(SaveLoadFailureReason.ValidationFailed);
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

    /// <summary>
    /// 세이브에 담긴 뮤테이터를 이 빌드가 전부 되살릴 수 있는지. <b>복원 착수 전에 부른다</b> -
    /// TryNormalize는 형태만 보고 카탈로그를 모르므로(DTO 계층이 게임 데이터 에셋을 알면 안 된다)
    /// 대조는 여기서 한다. "한 번 복원을 시작하면 롤백이 불가능하므로 그 전에 전부 막는다"는
    /// TryNormalize의 기존 계약을 그대로 이어받는 자리다.
    ///
    /// 이 경로는 <b>빌드 다운그레이드(구 빌드로 신 세이브 열기)에서만</b> 발생하고,
    /// <b>안전한 쪽으로 실패한다</b> - 통과시키면 제약이 조용히 빠져 난이도가 낮아지고,
    /// 저장된 난이도 점수와 실제 난이도가 어긋난 런이 된다.
    /// </summary>
    private bool CanRestoreMutators(List<RunMutatorSelectionDto> mutators)
    {
        List<(string Id, int Tier)> selections = SaveRestore.ToMutatorSelections(mutators);

        if (selections.Count == 0)
        {
            return true;
        }

        // 뮤테이터가 담긴 세이브인데 서비스가 없는 씬이면 적용할 방법이 없다.
        // 제약 없이 이어가는 것보다 열지 않는 편이 안전하다.
        if (_runModifierService == null)
        {
            Debug.LogError(
                $"[SaveService] 이 세이브에는 뮤테이터 {selections.Count}개가 담겨 있지만 "
                + $"{nameof(_runModifierService)}가 연결되지 않아 적용할 수 없습니다 - 불러오기를 거부합니다.",
                this);

            return false;
        }

        if (!_runModifierService.CanApplyRestoredMutators(selections))
        {
            Debug.LogError(
                "[SaveService] 세이브에 이 빌드가 모르는 뮤테이터 id 또는 범위 밖 단계가 있습니다 - 불러오기를 거부합니다.",
                this);

            return false;
        }

        return true;
    }

    // --- 내부 헬퍼 ---

    private void HandleDaySettled(int dayNumber)
    {
        if (!_isAutoSaveEnabled || _isRestoring)
        {
            return;
        }

        // 1일차 시작은 저장하지 않는다. 새 게임은 씬을 새로 열어 1일차부터 시작하는데, 여기서
        // 저장하면 직전 런의 이어하기(slot_00)가 아직 아무것도 진행하지 않은 상태로 덮어써진다.
        // 새 런은 2일차가 시작될 때 처음 기록된다.
        if (dayNumber <= SaveValidation.FIRST_DAY_NUMBER)
        {
            return;
        }

        // 철인 모드에서 슬롯이 고정된 뒤에는 자동저장도 그 슬롯으로 간다 - 0번에 따로 쌓이면
        // "세이브 하나"라는 규칙이 무의미해진다.
        AutoSaveAsync(EffectiveIronmanSlotIndexOrAutoSlot, this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// 밤 진입 직전 자동저장(철인 모드 전용). 낮에 한 건설·배치·연구를 되돌릴 수 없게 만드는 것이
    /// 이 모드의 요점이다.
    ///
    /// <b>OnNightStart가 아니라 OnDayEnd에 걸어야 한다.</b> CycleManager.StartNight가 CurrentCycle을
    /// Night로 바꾼 뒤 OnNightStart를 쏘므로, 그쪽에 걸면 CanSave가 거짓이 되거나 밤 스냅샷이 만들어져
    /// TryNormalize가 거부하는 파일이 된다. OnDayEnd는 StartNight 바로 앞이라 아직 Day다.
    /// SaveAsync의 캡처·직렬화는 동기라 밤이 시작되기 전에 스냅샷이 확정된다(디스크 쓰기만 await).
    ///
    /// HandleDaySettled의 1일차 제외(계약 2-1)는 여기 옮기지 않는다. 그 제외는 게임오버 후 Restart가
    /// 직전 런의 이어하기를 덮어쓰는 것을 막으려는 것인데, 철인 모드는 게임오버 시 그 세이브를 이미
    /// 지운다. 반대로 1일차의 밤 진입은 되돌릴 수 없게 만들 대상 그 자체다.
    /// 일차 인자는 쓰지 않는다 - 저장 시점은 날짜와 무관하다(TowerMorningRestoreSystem.RestoreAll과 같은 표기).
    /// </summary>
    private void HandleDayEnd(int _)
    {
        if (!_isAutoSaveEnabled || _isRestoring || !IsIronman)
        {
            return;
        }

        AutoSaveAsync(EffectiveIronmanSlotIndex, this.GetCancellationTokenOnDestroy()).Forget();
    }

    /// <summary>
    /// 철인 모드에서 게임오버가 나면 그 런의 세이브를 지운다. 승리에는 지우지 않는다.
    /// 삭제 사실을 이벤트와 플래그로 함께 알린다 - 조용히 사라지면 버그로 읽힌다.
    ///
    /// 지우는 것은 "유효 슬롯" 하나뿐이다. 고정 전 자동저장이 0번에 남아 있고 이후 다른 슬롯으로
    /// 고정된 런이라면 0번 파일이 남는데, 요청받지 않은 슬롯까지 지우는 것이 더 나쁘다.
    /// </summary>
    private void HandleGameOver()
    {
        if (!IsIronman)
        {
            return;
        }

        int slotIndex = EffectiveIronmanSlotIndex;

        if (!TryDelete(slotIndex))
        {
            Debug.LogWarning($"[SaveService] 철인 모드 - 슬롯 {slotIndex} 삭제에 실패했습니다.");
            return;
        }

        Debug.Log($"[SaveService] 철인 모드 - 게임오버로 슬롯 {slotIndex}의 세이브를 삭제했습니다.");
        WasIronmanSaveDeleted = true;
        _ironmanSaveDeleted.Invoke();
    }

    // 철인 모드가 아니면 언제나 자동저장 슬롯이다. 철인이면 고정 슬롯(미고정 시 자동저장 슬롯).
    private int EffectiveIronmanSlotIndexOrAutoSlot =>
        IsIronman ? EffectiveIronmanSlotIndex : AUTO_SAVE_SLOT_INDEX;

    private async UniTaskVoid AutoSaveAsync(int slotIndex, CancellationToken cancellationToken)
    {
        SaveResult result = await SaveAsync(slotIndex, true, cancellationToken);

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
    // TryWriteProtectedAtomic이 태우는 SaveCrypto도 Unity API를 만지지 않으므로 여기서 안전하다.
    // 본문을 먼저 확정한 뒤 메타를 쓴다 - 반대 순서면 "새 메타 + 옛 본문" 상태가 잠깐 생긴다.
    private static string WriteSlotFiles(
        string savePath,
        string metaPath,
        string thumbnailPath,
        string saveJson,
        string metaJson,
        byte[] thumbnailPng)
    {
        if (!SaveFileStore.TryWriteProtectedAtomic(savePath, saveJson, out string error))
        {
            return error;
        }

        // 메타는 파생 캐시이므로 실패해도 저장 자체는 성공으로 본다.
        // 다음 슬롯 조회 때 본문에서 다시 만들어진다(TryGetSlot 참고).
        SaveFileStore.TryWriteProtectedAtomic(metaPath, metaJson, out _);

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
        _babyDragonPlacementCoordinator,
        _runModifierService);
}
