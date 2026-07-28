using UnityEngine;

// 어미용 불 각성(모든 적에게 화상) - 활성 속성일 때 스폰되는 모든 몬스터에 상태이상을 얹는다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Spawn Status",
    fileName = "DragonSpawnStatusEffect")]
public sealed class DragonSpawnStatusEffectSO : DragonSkillEffectSO
{
    [SerializeField] private StatusEffectSO _status;

    public override StatusEffectSO GetSpawnStatus(DragonType? activeAttribute) =>
        IsActive(activeAttribute) ? _status : null;
}
