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

    public GridMap Map;
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

}

[Serializable]
public class DragonEgg
{
    public DragonType DragonType;
    public int FedDayCount; // 부화까지 성공적으로 먹은 날 수 누적
}