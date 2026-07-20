using System;

<<<<<<< HEAD
// 자원 종류. 점령 해금 표시(다중 플래그)와 보유량 관리(단일 비트)에 공용으로 쓴다.
// 인구는 실수량 보상(ResourceManager.AddPopulation)으로 별도 처리하므로 제외한다.
=======
// 점령 보상 중 해금 자원 종류 표시용. 인구는 실수량 보상(TempResourcePool.GrantPopulation)으로 별도 처리하므로 제외한다.
>>>>>>> origin/feature/64-population-maintenance
[Flags]
public enum ResourceType
{
    None = 0,
<<<<<<< HEAD

    // 기본 자원 (중앙 초원)
    Food = 1 << 0,             // 식량
    Wood = 1 << 1,             // 통나무
    Stone = 1 << 2,            // 돌

    // 특화 자원 (외곽 바이옴 - 점령 필요)
    FlameHeart = 1 << 3,       // 불꽃의 심장 (용암)
    SnowCrystal = 1 << 4,      // 눈의 결정 (설원)
    TimeSand = 1 << 5,         // 시간의 모래 (사막)
    PhilosopherStone = 1 << 6, // 현자의 돌 (암석)
=======
    Food = 1 << 0,
    Wood = 1 << 1,
    Stone = 1 << 2,
    Ore = 1 << 3,
>>>>>>> origin/feature/64-population-maintenance
}
