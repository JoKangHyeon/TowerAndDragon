using System;
using UnityEngine;

// 지역(지형) 하나가 그 지역 안의 건물 전체에 동일하게 매기는 페널티 원본 값이다.
// 셀마다 다른 값을 갖지 않는다 - 건물이 실제로 받는 양은 TerrainPenaltySystem이 풋프린트의
// 지형 비율과 외부 완화 배율을 곱해 따로 계산한다.
[Serializable]
public struct TerrainPenaltyEntry
{
    [Tooltip("이 지역 건물의 생산량을 깎는 비율. 0.3이면 -30%.")]
    [Range(0f, 1f)]
    public float YieldReductionRatio;

    [Tooltip("이 지역 타워의 공격속도를 깎는 비율. 0.3이면 -30%(발사 간격이 그만큼 늘어난다).")]
    [Range(0f, 1f)]
    public float TowerAttackSpeedReductionRatio;

    [Tooltip("이 지역 시설에 배치된 인구 1명당 매일 추가로 소모하는 나무.")]
    [Min(0)]
    public int WoodUpkeepPerPopulation;

    [Tooltip("이 지역 시설에 배치된 인구 1명당 매일 추가로 소모하는 돌.")]
    [Min(0)]
    public int StoneUpkeepPerPopulation;
}
