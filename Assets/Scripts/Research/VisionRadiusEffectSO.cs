using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Vision Radius",
    fileName = "VisionRadiusEffect")]
public sealed class VisionRadiusEffectSO : ResearchEffectSO
{
    [Min(0)]
    [SerializeField] private int _bonusRadius;

    public int BonusRadius => _bonusRadius;

    public override int GetVisionRadiusBonus()
    {
        return _bonusRadius;
    }
}
