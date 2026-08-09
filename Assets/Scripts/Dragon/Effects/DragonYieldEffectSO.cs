using UnityEngine;

// 어미용 암석(광산↑)·생명(농장↑) 각성/강화/궁극 공용 효과. 활성 속성일 때만 발동한다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Yield",
    fileName = "DragonYieldEffect")]
public sealed class DragonYieldEffectSO : DragonSkillEffectSO
{
    [SerializeField] private ResourceType _targetResources;

    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    public override float GetYieldMultiplierBonus(
        DragonType? activeAttribute,
        Vector2Int chunkCoord,
        ResourceType resourceType)
    {
        bool isTargetResource =
            resourceType != ResourceType.None &&
            (_targetResources & resourceType) == resourceType;

        return isTargetResource ? _bonusRatio * Scale(activeAttribute) : 0f;
    }
}
