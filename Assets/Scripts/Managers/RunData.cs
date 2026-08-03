using System;
using System.Collections.Generic;
using UnityEngine.Events;

[Serializable]
public class RunData
{
    public int CurrentCycle;

    public Dragon CurrentDragon;
    public List<BabyDragon> BabyDragons = new();
    public List<DragonEgg> DragonEggs = new();
    public List<BossDragonEggReward> BossDragonEggRewards = new();

    public UnityEvent OnInventoryChanged = new();

    public GridMap Map;

    public bool HasClaimedBossDragonEggReward(int cycleNumber)
    {
        if (BossDragonEggRewards == null)
        {
            return false;
        }

        foreach (BossDragonEggReward reward in BossDragonEggRewards)
        {
            if (reward != null && reward.CycleNumber == cycleNumber)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryRecordBossDragonEggReward(int cycleNumber, DragonType dragonType)
    {
        if (HasClaimedBossDragonEggReward(cycleNumber))
        {
            return false;
        }

        BossDragonEggRewards ??= new List<BossDragonEggReward>();
        BossDragonEggRewards.Add(new BossDragonEggReward
        {
            CycleNumber = cycleNumber,
            DragonType = dragonType,
        });
        return true;
    }
}


public enum DragonType
{
    Ice,
    Fire,
    Time,
    Stone,
    Life
}

[Serializable]
public class Dragon
{
    public DragonType CurrentType;
    public bool IsChangedThisDay;

    public UnityAction OnDragonTypeChanged;

    private CycleManager _cyclemanager;

    public void Construct(CycleManager cycleManager)
    {
        _cyclemanager = cycleManager;
        _cyclemanager.OnDayStart.AddListener(ResetChangedThisDay);
    }

    public bool TryChangeType(DragonType type)
    {
        if (IsChangedThisDay)
            return false;

        CurrentType = type;
        IsChangedThisDay = true;
        return true;
    }

    public void ResetChangedThisDay(int _)
    {
        IsChangedThisDay = false;
    }
}

[Serializable]
public class BabyDragon
{
    public string DragonName;
    public DragonType DragonType;

    public bool IsInTower;

    // 버프모드/공격모드 선택 - 철거 후 재설치해도 유지되도록 레코드에 저장한다.
    // IsModeInitialized가 false인 동안은 Mode(기본값 Attack)가 "아직 정해지지 않음"을 뜻한다 -
    // BabyDragonTower.BindRecord가 데이터 기반 기본값(공격 불가면 Buff)으로 한 번 채운다.
    public BabyDragonMode Mode;
    public bool IsModeInitialized;
}

[Serializable]
public class DragonEgg
{
    public DragonType DragonType;
    public int FedDayCount; 
}

[Serializable]
public sealed class BossDragonEggReward
{
    public int CycleNumber;
    public DragonType DragonType;
}
