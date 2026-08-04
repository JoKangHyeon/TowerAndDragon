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

        bool changed = false;
        foreach (DragonEgg egg in snapshot)
        {
            changed |= Grow(egg);
        }

        // 하루치 성장을 다 처리한 뒤 한 번만 알린다.
        // 부화뿐 아니라 '진행도만 오른' 경우도 알림 대상이다 - UI의 부화 진행도 표시가 갱신되어야 한다.
        // 알마다 발화하면 알이 여러 개일 때 같은 날에 UI가 여러 번 재구성된다.
        if (changed)
        {
            _gameManager.CurrentRun.OnInventoryChanged.Invoke();
        }
    }

    // 이 알이 실제로 변경(진행도 증가 또는 부화)되었으면 true - 호출자가 알림 발행 여부를 정한다.
    private bool Grow(DragonEgg egg)
    {
        if (!_dataCatalog.TryResolve(egg.DragonType, out BabyDragonData data))
        {
            Debug.LogError($"[DragonEggInventorySystem] 속성 {egg.DragonType}에 대응하는 BabyDragonData가 카탈로그에 없습니다.");
            return false;
        }

        // 초기 지급 등으로 FedDayCount가 이미 목표치를 채운 알은 바로 부화시킨다.
        if (egg.FedDayCount >= data.DaysToHatch)
        {
            Hatch(egg);
            return true;
        }

        egg.FedDayCount += 1;

        if (egg.FedDayCount < data.DaysToHatch)
        {
            // 진행도만 올랐다 - 부화는 아니지만 남은 일수 표시가 바뀌므로 변경으로 취급한다.
            return true;
        }

        Hatch(egg);
        return true;
    }

    // 알림은 호출자(GrowAll)가 하루치를 모두 처리한 뒤 한 번만 발행한다 - 여기서 발화하지 않는다.
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
