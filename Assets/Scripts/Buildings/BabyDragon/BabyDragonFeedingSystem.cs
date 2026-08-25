using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 새끼용의 슬라임 먹이를 매일 아침 지불한다. 필요량 전액을 지불하지 못하면 그날은
/// 가동하지 않는다(부분 가동 없음).
/// 프리팹은 씬 오브젝트를 참조할 수 없으므로, 다른 코디네이터들과 같은 방식으로
/// GridMap.OnBuildingAdded/Removing을 구독해 새끼용을 등록한다.
/// 지불은 CycleManager.OnDayStartUpkeep(생산 정산 다음 단계)에서 이루어지므로,
/// Factory의 당일 생산분이 이미 반영된 재고를 기준으로 판정한다.
///
/// 굶주림은 슬라임 비용의 후불 미결제 상태다 - 설치된 새끼용을 전부 먹인 뒤 슬라임이
/// 남으면 그 여분으로 인벤토리에 있는 미결제(굶주린) 새끼용을 마저 갚는다(SettleUnpaidInventoryFeed).
/// 설치된 새끼용이 우선이므로 순서를 바꾸지 않는다.
/// </summary>
public class BabyDragonFeedingSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private BabyDragonDataCatalog _dataCatalog;

    private readonly List<BabyDragonTower> _babyDragons = new();

    // 아침마다 한 번 갱신하는 속성별 설치 수 - 새끼용 하나하나가 리스트를 다시 순회하지 않게 한다.
    private readonly Dictionary<DragonType, int> _installedCountByDragonType = new();

    // 위 집계와 같은 순회에서 나오는 전체 마릿수. 예상치(ResourceForecast)와 같은 기준을 쓰기 위해
    // _babyDragons.Count 대신 BabyDragonFeedProjection이 돌려준 값을 그대로 쓴다.
    private int _installedTotalCount;

    private void OnEnable()
    {
        if (WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
            _gridMap.OnBuildingRemoving.AddListener(HandleBuildingRemoving);
        }

        if (WiringGuard.Require(_cycleManager, nameof(_cycleManager), this))
        {
            _cycleManager.OnDayStartUpkeep.AddListener(FeedAll);
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
            _cycleManager.OnDayStartUpkeep.RemoveListener(FeedAll);
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

    // OnDayStartUpkeep의 일차 인자는 쓰지 않는다 - 먹이량은 날짜와 무관하다.
    private void FeedAll(int _)
    {
        if (!WiringGuard.Require(_resourceManager, nameof(_resourceManager), this))
        {
            return;
        }

        RefreshInstalledCounts();

        foreach (BabyDragonTower babyDragon in _babyDragons)
        {
            Feed(babyDragon);
        }

        SettleUnpaidInventoryFeed();
    }

    // 집계는 예상치(ResourceForecast)와 공유한다 - 마릿수 세는 기준이 갈리면 표시값과
    // 실제 차감액이 조용히 어긋난다.
    private void RefreshInstalledCounts()
    {
        BabyDragonFeedProjection.AccumulateInstalledCounts(
            _babyDragons,
            _installedCountByDragonType,
            out _installedTotalCount);
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
        int requiredFeed = BabyDragonFeedFormula.ResolveDailyFeed(data, sameTypeCount, _installedTotalCount);

        if (requiredFeed <= 0)
        {
            babyDragon.SetFed(true);
            return;
        }

        if (!DragonSlimeTable.TryGetFeedSlime(data.DragonType, out ResourceType slimeType))
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
            $" + 전체 {_installedTotalCount} 추가분 {data.AdditionalFeedPerTotal})" +
            $" / 지불 {isFed}, 잔량 {_resourceManager.GetAmount(slimeType)}",
            babyDragon);
    }

    // 설치된 새끼용을 전부 먹인 뒤 남은 슬라임으로, 인벤토리에서 미결제(굶주린) 상태인 새끼용을
    // 마저 갚는다. 비용은 UI_DragonInventoryWindow가 슬롯에 보여주는 미리보기와 같은 식이다
    // (BabyDragonFeedFormula의 계약상 "이 용까지 포함해서 설치됐다고 가정했을 때"의 마릿수를 쓴다) -
    // 표시값과 실제 차감액이 갈리면 안 되기 때문이다.
    // GameManager·BabyDragonDataCatalog가 미연결인 씬(테스트 등)에서는 이 단계만 건너뛴다.
    private void SettleUnpaidInventoryFeed()
    {
        if (!WiringGuard.Require(_gameManager, nameof(_gameManager), this) ||
            !WiringGuard.Require(_dataCatalog, nameof(_dataCatalog), this))
        {
            return;
        }

        bool anySettled = false;

        foreach (BabyDragon record in _gameManager.CurrentRun.BabyDragons)
        {
            if (record.IsInTower || record.IsFed)
            {
                continue;
            }

            if (!_dataCatalog.TryResolve(record.DragonType, out BabyDragonData data))
            {
                continue;
            }

            _installedCountByDragonType.TryGetValue(record.DragonType, out int sameTypeCount);
            int requiredFeed = BabyDragonFeedFormula.ResolveDailyFeed(
                data, sameTypeCount + 1, _installedTotalCount + 1);

            if (requiredFeed <= 0)
            {
                record.IsFed = true;
                anySettled = true;
                continue;
            }

            if (!DragonSlimeTable.TryGetFeedSlime(record.DragonType, out ResourceType slimeType))
            {
                continue;
            }

            if (_resourceManager.TrySpend(slimeType, requiredFeed))
            {
                record.IsFed = true;
                anySettled = true;

                Debug.Log(
                    $"[BabyDragonFeedingSystem] 인벤토리 {record.DragonType} 미결제 {requiredFeed} 정산 완료" +
                    $" / 잔량 {_resourceManager.GetAmount(slimeType)}");
            }
        }

        if (anySettled)
        {
            _gameManager.CurrentRun.OnInventoryChanged.Invoke();
        }
    }
}
