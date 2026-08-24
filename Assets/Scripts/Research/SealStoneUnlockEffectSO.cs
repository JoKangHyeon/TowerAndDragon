using UnityEngine;

// 봉인석 건설을 해금한다. 대상 에셋을 고를 필요가 없어(봉인석은 한 종류) 값 없는 표식 효과다.
// TowerUnlockEffectSO가 TowerData를 지목하는 것과 대비되는 형태.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Seal Stone Unlock",
    fileName = "SealStoneUnlockEffect")]
public sealed class SealStoneUnlockEffectSO : ResearchEffectSO
{
    public override bool UnlocksSealStone()
    {
        return true;
    }
}
