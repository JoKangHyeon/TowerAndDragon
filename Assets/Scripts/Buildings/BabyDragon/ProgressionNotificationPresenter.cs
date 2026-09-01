using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DragonEggInventorySystem의 획득/부화 이벤트를 구독해 UI_ConfirmNotificationToast에 문구를 넘긴다.
/// 토스트가 새끼용 도메인을 몰라도 되게 하는 얇은 연결 계층 - GrantEgg 관문 하나에만 이벤트를 붙였으므로
/// 향후 점령·랜드마크 등 알 획득 경로가 늘어나도 이 브리지를 거쳐 알림이 자동으로 따라온다.
/// </summary>
public class ProgressionNotificationPresenter : MonoBehaviour
{
    private const string EGG_GRANTED_LOC_KEY = "baby_dragon_toast_egg_granted";
    private const string EGG_HATCHED_LOC_KEY = "baby_dragon_toast_egg_hatched";
    private const string ARTIFACT_ACQUIRED_LOC_KEY = "landmark_toast_artifact_acquired";
    private const string TOWER_UNLOCKED_LOC_KEY = "research_toast_tower_unlocked";

    [SerializeField] private DragonEggInventorySystem _eggInventorySystem;
    [SerializeField] private UI_ConfirmNotificationToast _toast;
    [SerializeField] private LandmarkManager _landmarkManager;
    [SerializeField] private ResearchManager _researchManager;
    [SerializeField] private CycleManager _cycleManager;

    // 프리팹 자산에는 씬 오브젝트(SaveService)를 꽂을 수 없어 항상 미배선으로 남는다 -
    // EnsureProgressionSystems가 런타임에 씬에서 찾는다(_landmarkManager 등과 같은 사정).
    [WiringOptional]
    [SerializeField] private SaveService _saveService;

    private readonly HashSet<string> _pendingArtifactIds = new();
    private readonly List<LandmarkDataSO> _pendingArtifacts = new();

    // "알렸다"가 아니라 "이미 해금돼 있어 알릴 필요가 없다"가 기준이다 - RebuildTowerBaseline이
    // 비우기와 재수립을 항상 함께 하므로, 이 집합을 다른 곳에서 단독으로 비우지 말 것(이슈 #286).
    private readonly HashSet<TowerData> _acknowledgedTowers = new();
    private readonly List<TowerData> _unlockedTowerBuffer = new();

    // RebuildTowerBaseline을 한 번도 돌리지 못한 상태에서는 토스트를 내지 않는다(fail-closed) -
    // 어떤 경로로 기준선이 비어도 이미 해금된 타워가 새 해금으로 되살아나지 않게 하는 마지막 방어선.
    private bool _hasTowerBaseline;

    /// <summary>부화 알림을 확인해 카드가 화면 밖으로 나간 뒤 발화한다.</summary>
    public event Action<DragonType> HatchNotificationDismissed;

    private void OnEnable()
    {
        EnsureEggInventorySystem();
        EnsureProgressionSystems();

        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.AddListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.AddListener(HandleEggHatched);
        }

        if (_landmarkManager != null)
        {
            _landmarkManager.OnLandmarkClaimed.AddListener(HandleLandmarkClaimed);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.AddListener(HandleResearchCompleted);
            _researchManager.ProgressRestored.AddListener(HandleResearchProgressRestored);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
        }

        if (_saveService != null)
        {
            _saveService.LoadCompleted.AddListener(HandleLoadCompleted);
        }
    }

    private void OnDisable()
    {
        if (_eggInventorySystem != null)
        {
            _eggInventorySystem.OnEggGranted.RemoveListener(HandleEggGranted);
            _eggInventorySystem.OnEggHatched.RemoveListener(HandleEggHatched);
        }

        if (_landmarkManager != null)
        {
            _landmarkManager.OnLandmarkClaimed.RemoveListener(HandleLandmarkClaimed);
        }

        if (_researchManager != null)
        {
            _researchManager.NodeCompleted.RemoveListener(HandleResearchCompleted);
            _researchManager.ProgressRestored.RemoveListener(HandleResearchProgressRestored);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }

        if (_saveService != null)
        {
            _saveService.LoadCompleted.RemoveListener(HandleLoadCompleted);
        }
    }

    private void Start()
    {
        // 새 런의 기준선(아직 해금된 특수 타워가 없다). 구독은 OnEnable, 첫 발화는 Start 이후라는
        // 규칙에 맞춘다. 이어하기라면 이 직후에 오는 HandleLoadCompleted가 다시 세운다.
        RebuildTowerBaseline();
    }

    private void HandleEggGranted(DragonType type)
    {
        if (_toast == null)
        {
            return;
        }

        _toast.ShowWithLocalizedArgument(
            EGG_GRANTED_LOC_KEY,
            ResolveEggSprite(type),
            DragonLocKeys.AttributeLocKey(type));
    }

    private void HandleEggHatched(DragonType type)
    {
        if (_toast == null)
        {
            HatchNotificationDismissed?.Invoke(type);
            return;
        }

        _toast.ShowWithLocalizedArgument(
            EGG_HATCHED_LOC_KEY,
            ResolveBabyDragonSprite(type),
            () => HatchNotificationDismissed?.Invoke(type),
            DragonLocKeys.AttributeLocKey(type));
    }

    // 알 알림에는 알 그림, 부화 알림에는 그 속성 새끼용 그림을 붙인다. 둘 다 BabyDragonData가 들고 있다.
    private Sprite ResolveEggSprite(DragonType type) =>
        TryResolveBabyDragonData(type, out BabyDragonData data) ? data.EggSprite : null;

    private Sprite ResolveBabyDragonSprite(DragonType type) =>
        TryResolveBabyDragonData(type, out BabyDragonData data) ? data.Sprite : null;

    private bool TryResolveBabyDragonData(DragonType type, out BabyDragonData data)
    {
        BabyDragonDataCatalog catalog = _eggInventorySystem?.DataCatalog;

        if (catalog == null)
        {
            data = null;
            return false;
        }

        return catalog.TryResolve(type, out data);
    }

    private void HandleLandmarkClaimed(LandmarkDataSO landmark)
    {
        if (!IsArtifactLandmark(landmark) || string.IsNullOrWhiteSpace(landmark.LandmarkId))
        {
            return;
        }

        if (_pendingArtifactIds.Add(landmark.LandmarkId))
        {
            _pendingArtifacts.Add(landmark);
        }
    }

    private void HandleDayStart(int dayNumber)
    {
        if (dayNumber == CycleManager.FIRST_DAY_NUMBER)
        {
            _pendingArtifactIds.Clear();
            _pendingArtifacts.Clear();
            RebuildTowerBaseline();
        }

        FlushArtifactNotifications();
        NotifyNewlyUnlockedTowers();
    }

    private void HandleResearchCompleted(ResearchNodeData _)
    {
        if (_researchManager != null && _researchManager.IsRestoringProgress)
        {
            return;
        }

        NotifyNewlyUnlockedTowers();
    }

    private void HandleResearchProgressRestored()
    {
        // 이 시점(세이브 복원 4단계)은 랜드마크 수령 이력 복원(7단계)보다 먼저 발화해, 유적
        // 조건이 걸린 타워는 전부 "미해금"으로 보인다(이슈 #286). 그래서 여기서 세우는 기준선은
        // 잠정치이고, 권위 있는 재수립은 복원이 모두 끝난 뒤 발화하는 HandleLoadCompleted가
        // 맡는다. SaveService가 없는 씬(튜토리얼 등)에 대한 보험으로 남겨 둔다.
        RebuildTowerBaseline();
    }

    // 세이브 복원 13단계와 ResumeDay가 모두 끝난 뒤 1회 발화한다(SaveService.LoadCompleted).
    // 유적 수령 이력까지 반영된 상태이므로 이 기준선이 권위를 가진다.
    private void HandleLoadCompleted()
    {
        RebuildTowerBaseline();
    }

    /// <summary>
    /// "이미 해금돼 있어 알릴 필요가 없는" 타워 기준선을 지금 상태로 다시 세운다.
    /// 비우기와 다시 채우기를 반드시 한 메서드 안에서 함께 한다 - 둘을 나누면 그 사이(또는
    /// 비우기만 도는 경로)에서 이미 해금된 타워가 새 해금으로 되살아난다(이슈 #286).
    /// </summary>
    private void RebuildTowerBaseline()
    {
        _acknowledgedTowers.Clear();
        _hasTowerBaseline = true;

        if (_researchManager == null)
        {
            return;
        }

        _researchManager.CollectUnlockedLandmarkTowers(_unlockedTowerBuffer);

        foreach (TowerData tower in _unlockedTowerBuffer)
        {
            if (tower != null)
            {
                _acknowledgedTowers.Add(tower);
            }
        }
    }

    private void FlushArtifactNotifications()
    {
        if (_toast == null || !_toast.CanShow || _landmarkManager == null)
        {
            return;
        }

        foreach (LandmarkDataSO landmark in _pendingArtifacts)
        {
            _toast.ShowWithLocalizedArgument(
                ARTIFACT_ACQUIRED_LOC_KEY,
                landmark.Icon,
                landmark.NameLocKey);
        }

        _pendingArtifactIds.Clear();
        _pendingArtifacts.Clear();
    }

    private void NotifyNewlyUnlockedTowers()
    {
        if (_researchManager == null)
        {
            return;
        }

        // 기준선을 한 번도 세우지 못한 상태에서는 절대 토스트를 내지 않는다 - 지금 해금된 것을
        // 전부 "이미 알고 있음"으로 접어 넣는 것이 유일하게 안전한 동작이다(이슈 #286 fail-closed).
        if (!_hasTowerBaseline)
        {
            RebuildTowerBaseline();
            return;
        }

        bool canShow = _toast != null && _toast.CanShow;

        _researchManager.CollectUnlockedLandmarkTowers(_unlockedTowerBuffer);

        foreach (TowerData tower in _unlockedTowerBuffer)
        {
            if (tower == null || string.IsNullOrWhiteSpace(tower.NameLocKey) ||
                _acknowledgedTowers.Contains(tower))
            {
                continue;
            }

            // 못 띄우는 상황이면 기록하지 않는다 - 여기서 먼저 기록하면 토스트가 꺼져 있던
            // 그 타워의 알림은 다시 뜰 기회 없이 영영 사라진다.
            if (!canShow)
            {
                continue;
            }

            _acknowledgedTowers.Add(tower);
            _toast.ShowWithLocalizedArgument(TOWER_UNLOCKED_LOC_KEY, tower.Icon, tower.NameLocKey);
        }
    }

    private static bool IsArtifactLandmark(LandmarkDataSO landmark)
    {
        if (landmark == null)
        {
            return false;
        }

        foreach (LandmarkRewardSO reward in landmark.ConquestRewards)
        {
            if (reward is ResearchUnlockRewardSO)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureEggInventorySystem()
    {
        if (_eggInventorySystem == null)
        {
            _eggInventorySystem = FindFirstObjectByType<DragonEggInventorySystem>();
        }
    }

    private void EnsureProgressionSystems()
    {
        if (_landmarkManager == null)
        {
            _landmarkManager = FindFirstObjectByType<LandmarkManager>();
        }

        if (_researchManager == null)
        {
            _researchManager = FindFirstObjectByType<ResearchManager>();
        }

        if (_cycleManager == null)
        {
            _cycleManager = FindFirstObjectByType<CycleManager>();
        }

        if (_saveService == null)
        {
            _saveService = FindFirstObjectByType<SaveService>();
        }
    }
}
