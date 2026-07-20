using System;
using UnityEngine;

// 자원 종류 + 수량 한 쌍. 초기 보유량, 향후 생산량/비용 표현의 공용 단위.
[Serializable]
public struct ResourceAmount
{
    [Tooltip("자원 종류(단일 종류만 지정).")]
    public ResourceType Type;

    public int Amount;
}
