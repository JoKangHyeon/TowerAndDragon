using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DragonEggInventorySystem의 획득/부화 이벤트를 구독해 UI_ConfirmNotificationToast에 문구를 넘긴다.
/// 토스트가 새끼용 도메인을 몰라도 되게 하는 얇은 연결 계층 - GrantEgg 관문 하나에만 이벤트를 붙였으므로
/// 향후 점령·랜드마크 등 알 획득 경로가 늘어나도 이 브리지를 거쳐 알림이 자동으로 따라온다.
/// </summary>
public class BabyDragonEggNotifier : MonoBehaviour
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

    private readonly HashSet<string> _pendingArtifactIds = new();
    private readonly List<LandmarkDataSO> _pendingArtifacts = new();
    private readonly HashSet<TowerData> _notifiedTowers = new();
    private readonly List<TowerData> _unlockedTowerBuffer = new();

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
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
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
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }
    }

    private void HandleEggGranted(DragonType type) => ShowToast(EGG_GRANTED_LOC_KEY, type);

    private void HandleEggHatched(DragonType type)
    {
        if (_toast == null)
        {
            HatchNotificationDismissed?.Invoke(type);
            return;
        }

        _toast.ShowWithLocalizedArgument(
            EGG_HATCHED_LOC_KEY,
            () => HatchNotificationDismissed?.Invoke(type),
            DragonLocKeys.AttributeLocKey(type));
    }

    private void ShowToast(string locKey, DragonType type)
    {
        if (_toast == null)
        {
            return;
        }

        _toast.ShowWithLocalizedArgument(locKey, DragonLocKeys.AttributeLocKey(type));
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

    private void HandleDayStart(int _)
    {
        if (_ == CycleManager.FIRST_DAY_NUMBER)
        {
            _pendingArtifactIds.Clear();
            _pendingArtifacts.Clear();
            _notifiedTowers.Clear();
        }

        FlushArtifactNotifications();
        NotifyNewlyUnlockedTowers();
    }

    private void HandleResearchCompleted(ResearchNodeData _)
    {
        NotifyNewlyUnlockedTowers();
    }

    private void FlushArtifactNotifications()
    {
        if (_toast == null || !_toast.isActiveAndEnabled || _landmarkManager == null)
        {
            return;
        }

        foreach (LandmarkDataSO landmark in _pendingArtifacts)
        {
            _toast.ShowWithLocalizedArgument(
                ARTIFACT_ACQUIRED_LOC_KEY,
                landmark.NameLocKey);
        }

        _pendingArtifactIds.Clear();
        _pendingArtifacts.Clear();
    }

    private void NotifyNewlyUnlockedTowers()
    {
        if (_toast == null || !_toast.isActiveAndEnabled || _researchManager == null)
        {
            return;
        }

        _researchManager.CollectUnlockedLandmarkTowers(_unlockedTowerBuffer);

        foreach (TowerData tower in _unlockedTowerBuffer)
        {
            if (tower == null || string.IsNullOrWhiteSpace(tower.NameLocKey) ||
                !_notifiedTowers.Add(tower))
            {
                continue;
            }

            _toast.ShowWithLocalizedArgument(TOWER_UNLOCKED_LOC_KEY, tower.NameLocKey);
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
    }
}
