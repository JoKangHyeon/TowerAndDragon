using UnityEngine;

// 액티브 스킬이 부여하는 상태이상을 더 강한 것으로 교체한다(얼음 "빙결 지속시간 증가" 등).
//
// 지속시간·틱데미지에 런타임 배율을 넣지 않고 상태이상 에셋 자체를 갈아끼우는 이유:
// MonsterStatusReceiver가 지속시간을 부여 시점에 복사하고 틱데미지는 매 틱 Source에서 다시
// 읽는 구조라, 배율을 도입하려면 엔트리 구조와 BaseMonster.ApplyStatus 시그니처까지 바뀐다.
// 랭크마다 값만 다른 StatusEffectSO를 하나 더 만들면 몬스터·상태 시스템을 전혀 건드리지 않는다.
//
// 어느 랭크의 상태를 쓸지는 DragonTreeManager가 노드 랭크로 고른다(높은 랭크 우선).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Skill Status",
    fileName = "DragonSkillStatusEffect")]
public sealed class DragonSkillStatusEffectSO : DragonSkillEffectSO
{
    [Tooltip("이 랭크에서 액티브 스킬이 대신 부여할 상태이상. 비우면 교체하지 않는다.")]
    [SerializeField] private StatusEffectSO _status;

    public override StatusEffectSO GetSkillStatusOverride(DragonType? activeAttribute, SkillSO skill) =>
        IsEffective(activeAttribute) ? _status : null;
}
