using System;

[Serializable]
public struct ResourceCost
{
    public int Population;
    public int Food;
    public int Wood;
    public int Stone;
    public int Ore; // 광물

    // this = 보유량, cost = 지불해야 할 비용. 모든 항목이 충족돼야 지불 가능.
    public bool CanAfford(ResourceCost cost) =>
        Population >= cost.Population &&
        Food >= cost.Food &&
        Wood >= cost.Wood &&
        Stone >= cost.Stone &&
        Ore >= cost.Ore;
}
