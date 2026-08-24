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

    public virtual bool UnlocksResourceNode(
        Vector2Int chunkCoord,
        TerrainType terrainType,
        ResourceType resourceType)
    {
        return false;
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

    // 최대체력만 소비 시점이 다르다 - Health가 최댓값을 값으로 들고 있어 매 공격마다 pull할 수 없고,
    // TowerMaxHealthApplier가 밤 시작에 한 번 읽어 확정한다.
    public virtual float GetTowerMaxHealthMultiplierBonus(TowerData towerData)
    {
        return 0f;
    }

    public virtual float GetConquestCostReductionRatio()
    {
        return 0f;
    }

    public virtual int GetConquestPopulationReduction()
    {
        return 0;
    }

    public virtual int GetConquestDaysReduction()
    {
        return 0;
    }

    public virtual float GetCastleDailyRegenAmount()
    {
        return 0f;
    }

    // producedResources는 생산 시설이 만드는 자원(없으면 None). 자원별 인력 효율 연구가
    // "식량 시설만" 같은 조건을 걸 수 있도록 배치 종류와 함께 넘긴다.
    public virtual int GetPopulationCapacityDelta(
        PopulationAssignmentType assignmentType,
        ResourceType producedResources)
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

    public virtual int GetResearchLabBuildLimitBonus()
    {
        return 0;
    }

    // 해금되는 타워. null이면 해금 효과가 아니다.
    // DragonSkillEffectSO.GetUnlockedSkill과 같은 형태 - ResearchManager가 pull한다.
    public virtual TowerData GetUnlockedTower()
    {
        return null;
    }

    // 봉인석 건설 해금 여부. 타워 해금과 달리 대상 에셋이 하나뿐이라 bool로 둔다.
    public virtual bool UnlocksSealStone()
    {
        return false;
    }

    public virtual bool UnlocksTowerCombatRepair()
    {
        return false;
    }
}
