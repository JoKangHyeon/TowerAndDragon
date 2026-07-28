using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 알(부화 전 새끼용)은 슬라임 소비 없이 매일 아침 day count만 증가시켜 성장시키고, 임계치 도달 시 부화시킨다.
/// 모든 알 획득 경로(시작 지급/디버그/추후 점령 보상)는 GrantEgg 하나만 호출하면 된다.
/// </summary>
public class DragonEggInventorySystem : MonoBehaviour
{
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private CycleManager _cycleManager;
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
        _gameManager.CurrentRun.OnInventoryChanged.Invoke();
    }

    // OnDayStart의 일차 인자는 쓰지 않는다 - 부화 속도는 날짜와 무관하다.
    private void GrowAll(int _)
    {
        if (_gameManager == null || _dataCatalog == null)
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

        // 초기 지급 등으로 FedDayCount가 이미 목표치를 채운 알은 바로 부화시킨다.
        if (egg.FedDayCount >= data.DaysToHatch)
        {
            Hatch(egg);
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
        _gameManager.CurrentRun.OnInventoryChanged.Invoke();

        Debug.Log($"[DragonEggInventorySystem] 알(속성 {egg.DragonType})이 부화했습니다.");
    }
}
