using System.Collections.Generic;
using UnityEngine;

// 포탈 봉인용 건물. 4개(1~4차)가 프리팹 1개 + SealStoneData 4종으로 존재한다 -
// 어느 차수를 쓰는지에 따라 비용이 고정으로 정해지므로 Factory/Tower와 동일한 패턴.
public sealed class SealStone : Building
{
    [SerializeField] private SealStoneData _data;

    public SealStoneData Data => _data;

    public override IReadOnlyList<ResourceAmount> BuildCost =>
        _data != null ? _data.BuildCost : base.BuildCost;
}
