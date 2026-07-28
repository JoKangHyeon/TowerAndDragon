using UnityEngine;

// 용 스킬 효과 베이스. ResearchEffectSO와 같은 "fat base + virtual 게터" 방식 -
// 파생 클래스는 자신이 담당하는 게터만 override하고 나머지는 기본값(무효과)을 그대로 쓴다.
// 활성 속성 판정은 각 효과가 스스로 한다(IsActive) - DragonTreeManager는 판정 없이 그대로 넘겨준다.
//
// activeAttribute를 DragonType?(nullable)로 받는 이유: DragonTreeManager.ActiveAttribute는
// RunData.CurrentDragon이 아직 없을 때 null일 수 있다(실측). null을 default(DragonType)로
// 대체하면 enum 0번 값(Ice)이 실수로 "활성"으로 취급된다 - IsActive가 null을 안전하게 항상
// false로 판정하도록 nullable을 그대로 전파한다.
public abstract class DragonSkillEffectSO : ScriptableObject
{
    [SerializeField] private DragonType _attribute;

    public DragonType Attribute => _attribute;

    protected bool IsActive(DragonType? activeAttribute) => activeAttribute.HasValue && activeAttribute.Value == _attribute;

    public virtual float GetTowerAttackSpeedMultiplierBonus(DragonType? activeAttribute, TowerData towerData) => 0f;
    public virtual float GetTowerDamageMultiplierBonus(DragonType? activeAttribute, TowerData towerData) => 0f;

    // TerrainType을 받지 않는 이유: 슬라임·특화 자원은 자원 종류 자체가 바이옴을 특정하므로
    // 지형 게이트가 중복이다(YieldMultiplierEffectSO._ignoresTerrain이 존재하는 이유와 동일).
    // DragonTreeManager.Construct에도 GridMap이 없어 지형을 해석할 수 없다.
    public virtual float GetYieldMultiplierBonus(DragonType? activeAttribute, Vector2Int chunkCoord, ResourceType resourceType) => 0f;

    public virtual int GetVisionRadiusBonus(DragonType? activeAttribute) => 0;
    public virtual float GetConquestCostReductionRatio(DragonType? activeAttribute) => 0f;
    public virtual int GetConquestDaysReduction(DragonType? activeAttribute) => 0;

    // 타워 기본공격이 명중할 때 대상에 얹을 상태이상(얼음 패시브·새끼용 타워형).
    public virtual StatusEffectSO GetTowerHitStatus(DragonType? activeAttribute, TowerData towerData) => null;

    // 몬스터 스폰 시점에 얹을 상태이상(불 패시브).
    public virtual StatusEffectSO GetSpawnStatus(DragonType? activeAttribute) => null;

    // 액티브 스킬 해금 - 노드가 참조하는 SkillSO. 활성 속성과 무관하게 해금은 영구 유지된다
    // (로드맵 §1-3) - DragonTreeManager.UnlockedActiveSkills가 이 값을 그대로 수집한다.
    public virtual SkillSO GetUnlockedSkill() => null;

    // 강화·궁극 노드가 액티브 스킬 위력을 강화하는 값(위력 배율 보너스 + 쿨다운 감소 비율).
    public virtual float GetSkillPowerMultiplierBonus(DragonType? activeAttribute, SkillSO skill) => 0f;
    public virtual float GetSkillCooldownReductionRatio(DragonType? activeAttribute, SkillSO skill) => 0f;

    // 새끼용 지역형(B 슬롯, 암석/생명) - BabyDragonBuffSystem의 반경 생산배율에 곱해질 추가 보너스.
    // 활성 속성과 무관하게 발동한다(로드맵 §1-3) - dragonType은 대상 새끼용의 속성이며,
    // 이 효과 자신의 Attribute와 일치할 때만 값을 반환하도록 구현이 스스로 게이트한다.
    public virtual float GetKinAreaYieldBonusRatio(DragonType dragonType) => 0f;
}
