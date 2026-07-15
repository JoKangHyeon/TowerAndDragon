using System;
using System.Collections.Generic;

[Serializable]
public class RunData
{
    public int CurrentCycle;

    public Dragon CurrentDragon;
    public List<BabyDragon> BabyDragons;

    public GridMap Map;

    public List<int> Resources;
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
}

public class BabyDragon
{
    public string DragonName;
    public DragonType DragonType;

    public bool IsInTower;

}