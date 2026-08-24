using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Tower Combat Repair Unlock",
    fileName = "TowerCombatRepairUnlockEffect")]
public sealed class TowerCombatRepairUnlockEffectSO : ResearchEffectSO
{
    public override bool UnlocksTowerCombatRepair()
    {
        return true;
    }
}
