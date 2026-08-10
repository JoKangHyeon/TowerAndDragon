using UnityEngine;

// 용 스킬 효과 베이스. ResearchEffectSO와 같은 "fat base + virtual 게터" 방식 -
// 파생 클래스는 자신이 담당하는 게터만 override하고 나머지는 기본값(무효과)을 그대로 쓴다.
// 활성 속성 판정은 각 효과가 스스로 한다 - DragonTreeManager는 판정 없이 그대로 넘겨준다.
//
// activeAttribute를 DragonType?(nullable)로 받는 이유: DragonTreeManager.ActiveAttribute는
// RunData.CurrentDragon이 아직 없을 때 null일 수 있다(실측). null을 default(DragonType)로
// 대체하면 enum 0번 값(Ice)이 실수로 "활성"으로 취급된다 - Scale이 null을 안전하게 항상
// 0으로 판정하도록 nullable을 그대로 전파한다.
//
// 활성/비활성 배율을 따로 두는 이유(빌드 다양성 설계):
// 궁극 노드가 "이 속성이 비활성일 때도 패시브가 절반 남는다"를 표현하려면, 잔존시킬 대상이
// 궁극 자신이 아니라 루트가 준 기본 패시브다. 궁극 노드에 같은 종류의 효과를
// (_suppressWhileActive=true, _inactiveScale=0.5)로 하나 더 붙이면 Aggregate가 합산해 주므로
// 매니저·쿼리 인터페이스를 전혀 건드리지 않고 표현된다.
public abstract class DragonSkillEffectSO : ScriptableObject
{
    [SerializeField] private DragonType _attribute;

    public DragonType Attribute => _attribute;

    // 두 필드 모두 "기본값(false / 0) = 기존 동작"이 되도록 잡았다.
    // _activeScale 같은 '기본값 1' 필드를 쓰면, 필드가 추가되기 전에 직렬화된 에셋에서
    // 키가 누락돼 0으로 읽힐 경우 모든 효과가 조용히 무효화된다 - 그 위험을 구조적으로 없앤다.
    [Tooltip("켜면 이 속성이 '활성일 때' 효과가 나오지 않는다. 궁극의 잔존 효과처럼 " +
        "비활성 전용으로 쓰는 효과에만 켠다(기본 패시브와 이중 계산되는 것을 막는다).")]
    [SerializeField] private bool _suppressWhileActive;

    [Tooltip("이 속성이 비활성일 때 남는 효과 비율. 0이 기존 동작(비활성이면 아무 효과 없음)이다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _inactiveScale;

    protected float Scale(DragonType? activeAttribute)
    {
        if (!activeAttribute.HasValue)
        {
            return 0f;
        }

        if (activeAttribute.Value != _attribute)
        {
            return _inactiveScale;
        }

        return _suppressWhileActive ? 0f : 1f;
    }

    // 상태이상처럼 배율을 곱할 수 없는 (객체를 반환하는 ) 효과용 -
    // 지금 상태에서 이 효과가 발동해야 하는지만 판정
    protected bool IsEffective(DragonType? activeAttribute) => Scale(activeAttribute) > 0f;

    public virtual float GetTowerAttackSpeedMultiplierBonus(DragonType? activeAttribute, TowerData towerData) => 0f;
    public virtual float GetTowerDamageMultiplierBonus(DragonType? activeAttribute, TowerData towerData) => 0f;

    //사거리 - 새끼용 타워형 (불 kinB)이 쓴다.
    public virtual float GetTowerRangeMultiplierBonus(DragonType? activeAttribute, TowerData towerData) => 0f;

    //타워 최대 체력 (생명 각성 & 방벽 강화) - 소비 시점이 Tower.Setup 1번 뿐
    // 따라서 밤 시작 시 재초기화하는 쪽에서 읽음
    public virtual float GetTowerMaxHealthMultiplierBonus(DragonType? activeAttribute, TowerData towerData) => 0f;

    public virtual float GetYieldMultiplierBonus(DragonType? activeAttribute, Vector2Int chunkCoord, ResourceType resourceType) => 0f;

    public virtual int GetVisionRadiusBonus(DragonType? activeAttribute) => 0;
    public virtual float GetConquestCostReductionRatio(DragonType? activeAttribute) => 0f;
    public virtual int GetConquestDaysReduction(DragonType? activeAttribute) => 0;

    // 타워 기본 공격이 명중할 때 대상에 얹을 상태이상 (얼음, 불)
    public virtual StatusEffectSO GetTowerHitStatus(DragonType? activeAttribute, TowerData towerData) => null;
     // 몬스터 스폰 시점에 얹을 상태이상(불 패시브).
    public virtual StatusEffectSO GetSpawnStatus(DragonType? activeAttribute) => null;

    // 액티브 스킬 해금 - 노드가 참조하는 SkillSO. 활성 속성과 무관하게 해금은 영구 유지된다
    // (잔존 배율과 무관 - 사용 가능 여부 필터링은 DragonTreeManager.AvailableActiveSkills가 한다).
    public virtual SkillSO GetUnlockedSkill() => null;

    // 강화·궁극 노드가 액티브 스킬 위력을 강화하는 값(위력 배율 보너스 + 쿨다운 감소 비율).
    public virtual float GetSkillPowerMultiplierBonus(DragonType? activeAttribute, SkillSO skill) => 0f;
    public virtual float GetSkillCooldownReductionRatio(DragonType? activeAttribute, SkillSO skill) => 0f;

    // 액티브 스킬의 일일 사용 횟수 추가분(암석 궁극).
    public virtual int GetSkillExtraUsePerDay(DragonType? activeAttribute, SkillSO skill) => 0;

    // 액티브 스킬이 부여할 상태이상 교체분(얼음 빙결 지속시간 강화).
    // 지속시간 배율 대신 더 강한 상태이상 에셋을 통째로 갈아끼우는 방식이다 -
    // 자세한 이유는 DragonSkillStatusEffectSO 주석 참고.
    public virtual StatusEffectSO GetSkillStatusOverride(DragonType? activeAttribute, SkillSO skill) => null;

    // 새끼용 지역형(B 슬롯) - BabyDragonBuffSystem의 반경 생산배율에 곱해질 추가 보너스.
    // 활성 속성과 무관하게 발동한다 - dragonType은 대상 새끼용의 속성이며,
    // 이 효과 자신의 Attribute와 일치할 때만 값을 반환하도록 구현이 스스로 게이트한다.
    public virtual float GetKinAreaYieldBonusRatio(DragonType dragonType) => 0f;

    // 새끼용 버프 반경 증가분(생명·시간 kin). 위와 같은 계약 - 활성 속성 무관.
    public virtual float GetKinBuffRadiusBonusRatio(DragonType dragonType) => 0f;

    // 암석 액티브가 설치하는 방벽의 최대체력 증가분.
    public virtual float GetBarricadeHealthBonusRatio(DragonType? activeAttribute) => 0f;
}
