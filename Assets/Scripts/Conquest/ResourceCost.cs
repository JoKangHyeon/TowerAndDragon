using System;

// 점령 출격 비용/보유량 스냅샷. 기획상 점령 비용은 인구 + 기본 자원만 쓴다(특화 자원 미포함).
[Serializable]
public struct ResourceCost
{
    public int Population;
    public int Food;
    public int Wood;
    public int Stone;

    // this = 보유량, cost = 지불해야 할 비용. 모든 항목이 충족돼야 지불 가능.
    public bool CanAfford(ResourceCost cost) =>
        Population >= cost.Population &&
        Food >= cost.Food &&
        Wood >= cost.Wood &&
        Stone >= cost.Stone;

    // this = 보유량에서 cost만큼 차감한 결과. 원정 발송 시 실제 차감에 사용.
    public ResourceCost Subtract(ResourceCost cost) => new ResourceCost
    {
        Population = Population - cost.Population,
        Food = Food - cost.Food,
        Wood = Wood - cost.Wood,
        Stone = Stone - cost.Stone,
    };
}
