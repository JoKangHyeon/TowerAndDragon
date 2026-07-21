using System;
using System.Collections.Generic;
using UnityEngine.Events;

[Serializable]
public class RunData
{
    public int CurrentCycle;

    public Dragon CurrentDragon;
    public List<BabyDragon> BabyDragons;

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

public class BabyDragon
{
    public string DragonName;
    public DragonType DragonType;

    public bool IsInTower;

}