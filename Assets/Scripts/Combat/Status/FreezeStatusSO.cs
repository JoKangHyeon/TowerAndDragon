using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Combat/Status/Freeze",
    fileName = "FreezeStatus"
)]

public sealed class FreezeStatusSO : StatusEffectSO
{
    // 이동과 행동을 모두 막으므로 군중제어다.
    public override bool IsCrowdControl => true;
}