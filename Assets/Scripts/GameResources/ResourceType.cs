using System;

// 자원 종류. 점령 해금 표시(다중 플래그)와 보유량 관리(단일 비트)에 공용으로 쓴다.
// 인구는 실수량 보상(ResourceManager.AddPopulation)으로 별도 처리하므로 제외한다.

[Flags]
public enum ResourceType
{
    None = 0,

    // 기본 자원 (중앙 초원)
    Food = 1 << 0,             // 식량
    Wood = 1 << 1,             // 통나무
    Stone = 1 << 2,            // 돌

    // 특화 자원 (외곽 바이옴 - 점령 필요)
    FlameHeart = 1 << 3,       // 불꽃의 심장 (용암)
    SnowCrystal = 1 << 4,      // 눈의 결정 (설원)
    TimeSand = 1 << 5,         // 시간의 모래 (사막)
    PhilosopherStone = 1 << 6, // 현자의 돌 (암석)

    // 슬라임 (지형별 하위 타입 - 용의 먹이, 생산시설 풋프린트에 걸친 종류별로 각각 합산되어 나온다)
    GrassSlime = 1 << 7,       // 초원 슬라임
    RockSlime = 1 << 8,        // 암석 슬라임
    VolcanoSlime = 1 << 9,     // 용암 슬라임
    DesertSlime = 1 << 10,     // 사막 슬라임
    SnowSlime = 1 << 11,       // 설원 슬라임
}
