using UnityEngine;

public abstract class ResearchEffectSO : ScriptableObject
{
    public virtual float GetYieldMultiplierBonus(
        Vector2Int chunkCoord,
        TerrainType terrainType,
        ResourceType resourceType)
    {
        return 0f;
    }

    public virtual float GetTowerDamageMultiplierBonus(TowerData towerData)
    {
        return 0f;
    }

    public virtual float GetTowerRangeMultiplierBonus(TowerData towerData)
    {
        return 0f;
    }

    public virtual float GetTowerAttackSpeedMultiplierBonus(TowerData towerData)
    {
        return 0f;
    }

    public virtual float GetConquestCostReductionRatio()
    {
        return 0f;
    }

    public virtual int GetConquestDaysReduction()
    {
        return 0;
    }

    public virtual float GetCastleDailyRegenAmount()
    {
        return 0f;
    }

    public virtual int GetPopulationCapacityDelta(PopulationAssignmentType assignmentType)
    {
        return 0;
    }

    public virtual int GetVisionRadiusBonus()
    {
        return 0;
    }

    public virtual int GetMoveAllowanceBonus()
    {
        return 0;
    }

    // 해금되는 타워. null이면 해금 효과가 아니다.
    // DragonSkillEffectSO.GetUnlockedSkill과 같은 형태 - ResearchManager가 pull한다.
    public virtual TowerData GetUnlockedTower()
    {
        return null;
    }
}
