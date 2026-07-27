using System.Collections.Generic;
using UnityEngine;

// 새끼용 슬롯 게이트 - 해당 속성 새끼용을 보유해야 해금 가능.
// RunData.BabyDragons가 런타임에 null일 수 있으므로(실측 - BabyDragon에 [Serializable] 없음)
// null이면 미보유로 취급한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Gates/Kin Owned",
    fileName = "KinOwnedGate")]
public sealed class KinOwnedGateSO : ProgressionGateSO
{
    [SerializeField] private DragonType _attribute;

    public override bool IsSatisfied(ProgressionContext context)
    {
        List<BabyDragon> babyDragons = context.Game != null ? context.Game.CurrentRun?.BabyDragons : null;

        if (babyDragons == null)
        {
            return false;
        }

        foreach (BabyDragon babyDragon in babyDragons)
        {
            if (babyDragon != null && babyDragon.DragonType == _attribute)
            {
                return true;
            }
        }

        return false;
    }
}
