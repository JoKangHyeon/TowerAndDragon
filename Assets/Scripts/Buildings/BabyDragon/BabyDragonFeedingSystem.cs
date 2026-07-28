using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 새끼용의 슬라임 먹이를 매일 아침 지불한다. 필요량 전액을 지불하지 못하면 그날은
/// 가동하지 않는다(부분 가동 없음).
/// 프리팹은 씬 오브젝트를 참조할 수 없으므로, 다른 코디네이터들과 같은 방식으로
/// GridMap.OnBuildingAdded/Removing을 구독해 새끼용을 등록한다.
/// </summary>
public class BabyDragonFeedingSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private ResourceManager _resourceManager;

    private readonly List<BabyDragonTower> _babyDragons = new();

    // 아침마다 한 번 갱신하는 속성별 설치 수 - 새끼용 하나하나가 리스트를 다시 순회하지 않게 한다.
    private readonly Dictionary<DragonType, int> _installedCountByDragonType = new();

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(FeedAll);
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.RemoveListener(HandleBuildingRemoving);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(FeedAll);
        }
    }

    // GridMap.OnBuildingAdded는 BabyDragonPlacementCoordinator도 같이 구독하며 Setup()으로
    // DragonData를 채운다. 두 리스너의 구독 순서(컴포넌트 순서)는 보장되지 않으므로, 여기서
    // DragonData 유무로 등록을 거부하면 Setup()이 아직 실행되지 않은 타이밍에 false negative가
    // 난다. 등록은 무조건 하고, 실제로 값이 필요한 FeedAll(아침 정산) 시점에만 null을 검사한다.
    private void HandleBuildingAdded(Building building)
    {
        if (!(building is BabyDragonTower babyDragon))
        {
            return;
        }

        if (_babyDragons.Contains(babyDragon))
        {
            return;
        }

        _babyDragons.Add(babyDragon);
    }

    private void HandleBuildingRemoving(Building building)
    {
        if (building is BabyDragonTower babyDragon)
        {
            _babyDragons.Remove(babyDragon);
        }
    }

    // OnDayStart의 일차 인자는 쓰지 않는다 - 먹이량은 날짜와 무관하다.
    private void FeedAll(int _)
    {
        if (_resourceManager == null)
        {
            return;
        }

        RefreshInstalledCounts();

        foreach (BabyDragonTower babyDragon in _babyDragons)
        {
            Feed(babyDragon);
        }
    }

    private void RefreshInstalledCounts()
    {
        _installedCountByDragonType.Clear();

        foreach (BabyDragonTower babyDragon in _babyDragons)
        {
            if (babyDragon.DragonData == null)
            {
                continue;
            }

            DragonType dragonType = babyDragon.DragonData.DragonType;
            _installedCountByDragonType.TryGetValue(dragonType, out int count);
            _installedCountByDragonType[dragonType] = count + 1;
        }
    }

    private void Feed(BabyDragonTower babyDragon)
    {
        BabyDragonData data = babyDragon.DragonData;
        if (data == null)
        {
            Debug.LogError(
                "[BabyDragonFeedingSystem] 새끼용에 BabyDragonData가 할당되지 않았습니다.",
                babyDragon);
            babyDragon.SetFed(false);
            return;
        }

        _installedCountByDragonType.TryGetValue(data.DragonType, out int sameTypeCount);
        int requiredFeed = BabyDragonFeedFormula.ResolveDailyFeed(data, sameTypeCount, _babyDragons.Count);

        if (requiredFeed <= 0)
        {
            babyDragon.SetFed(true);
            return;
        }

        if (!BabyDragonSlimeTable.TryGetFeedSlime(data.DragonType, out ResourceType slimeType))
        {
            Debug.LogError(
                $"[BabyDragonFeedingSystem] 속성 {data.DragonType}에 대응하는 먹이 슬라임이 없습니다.",
                babyDragon);
            babyDragon.SetFed(false);
            return;
        }

        // TrySpend는 보유량이 부족하면 부분 차감 없이 false를 반환한다 - 전부-또는-전무 판정에 그대로 쓴다.
        bool isFed = _resourceManager.TrySpend(slimeType, requiredFeed);
        babyDragon.SetFed(isFed);

        Debug.Log(
            $"[BabyDragonFeedingSystem] {babyDragon.name} 먹이 {slimeType}" +
            $" 필요 {requiredFeed} (기본 {data.BaseFeed}" +
            $" + 같은속성 {sameTypeCount} 추가분 {data.AdditionalFeedPerSameType}" +
            $" + 전체 {_babyDragons.Count} 추가분 {data.AdditionalFeedPerTotal})" +
            $" / 지불 {isFed}, 잔량 {_resourceManager.GetAmount(slimeType)}",
            babyDragon);
    }
}
