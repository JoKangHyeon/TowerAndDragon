using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 알(부화 전 새끼용)의 슬라임 먹이를 매일 아침 지불해 성장시키고, 임계치 도달 시 부화시킨다.
/// 모든 알 획득 경로(시작 지급/디버그/추후 점령 보상)는 GrantEgg 하나만 호출하면 된다.
/// </summary>
public class DragonEggInventorySystem : MonoBehaviour
{
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private BabyDragonDataCatalog _dataCatalog;

    // 디버그 GUI가 알의 부화 진행도(며칠째/목표 며칠)를 표시할 때 카탈로그를 다시 참조로 안 받고 이걸 쓴다.
    public BabyDragonDataCatalog DataCatalog => _dataCatalog;

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(GrowAll);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(GrowAll);
        }
    }

    public void GrantEgg(DragonType dragonType)
    {
        _gameManager.CurrentRun.DragonEggs.Add(new DragonEgg { DragonType = dragonType });
    }

    // OnDayStart의 일차 인자는 쓰지 않는다 - 부화 속도는 날짜와 무관하다.
    private void GrowAll(int _)
    {
        if (_gameManager == null || _resourceManager == null || _dataCatalog == null)
        {
            return;
        }

        // 부화로 원본 리스트가 변경되므로 스냅샷을 순회한다.
        DragonEgg[] snapshot = _gameManager.CurrentRun.DragonEggs.ToArray();

        foreach (DragonEgg egg in snapshot)
        {
            Grow(egg);
        }
    }

    private void Grow(DragonEgg egg)
    {
        if (!_dataCatalog.TryResolve(egg.DragonType, out BabyDragonData data))
        {
            Debug.LogError($"[DragonEggInventorySystem] 속성 {egg.DragonType}에 대응하는 BabyDragonData가 카탈로그에 없습니다.");
            return;
        }

        // 초기 지급 등으로 FedDayCount가 이미 목표치를 채운 알은 그날 먹이 소비 성공 여부와
        // 무관하게 바로 부화시킨다 - 이미 달성한 조건을 매일 재확인시키면 부화가 불필요하게 늦어진다.
        if (egg.FedDayCount >= data.DaysToHatch)
        {
            Hatch(egg);
            return;
        }

        bool isFed = data.EggDailyFeed <= 0;

        if (!isFed)
        {
            if (!BabyDragonSlimeTable.TryGetFeedSlime(egg.DragonType, out ResourceType slimeType))
            {
                Debug.LogError($"[DragonEggInventorySystem] 속성 {egg.DragonType}에 대응하는 먹이 슬라임이 없습니다.");
                return;
            }

            // TrySpend는 부족하면 부분 차감 없이 false를 반환한다 - 전부-또는-전무로 그대로 쓴다.
            isFed = _resourceManager.TrySpend(slimeType, data.EggDailyFeed);
        }

        if (!isFed)
        {
            return;
        }

        egg.FedDayCount += 1;

        if (egg.FedDayCount < data.DaysToHatch)
        {
            return;
        }

        Hatch(egg);
    }

    private void Hatch(DragonEgg egg)
    {
        _gameManager.CurrentRun.DragonEggs.Remove(egg);
        _gameManager.CurrentRun.BabyDragons.Add(new BabyDragon
        {
            DragonType = egg.DragonType,
            IsInTower = false,
        });

        Debug.Log($"[DragonEggInventorySystem] 알(속성 {egg.DragonType})이 부화했습니다.");
    }
}
