using System;

// 점령 보상 중 해금 자원 종류 표시용. 인구는 실수량 보상(TempResourcePool.GrantPopulation)으로 별도 처리하므로 제외한다.
[Flags]
public enum ResourceType
{
    None = 0,
    Food = 1 << 0,
    Wood = 1 << 1,
    Stone = 1 << 2,
    Ore = 1 << 3,
}
