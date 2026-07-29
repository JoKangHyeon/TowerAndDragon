using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 번의 공격 적용에 실리는 부가 정보. 효과가 시전자 위치·소유자 등을
/// 필요로 할 때 참조한다. 지금은 시전자·공격력 보정·부가 상태이상을 담는다.
/// </summary>
public readonly struct AttackContext
{
    public GameObject Source { get; }
    public ResolvedEnemyStatModifier AttackPowerModifier { get; }

    // 명중 시점에 대상에 추가로 얹을 상태이상(용 스킬트리 얼음 패시브 등).
    // 발사 시점(TowerAttack.Fire)이 아니라 명중 시점(AttackSO.Execute)에 적용해야
    // 투사체가 명중할 때까지 지연되는 타이밍과 어긋나지 않는다.
    public IReadOnlyList<StatusEffectSO> ExtraStatuses { get; }

    public AttackContext(GameObject source)
        : this(source, ResolvedEnemyStatModifier.Neutral, null)
    {
    }

    public AttackContext(
        GameObject source,
        ResolvedEnemyStatModifier attackPowerModifier)
        : this(source, attackPowerModifier, null)
    {
    }

    public AttackContext(
        GameObject source,
        ResolvedEnemyStatModifier attackPowerModifier,
        IReadOnlyList<StatusEffectSO> extraStatuses)
    {
        Source = source;
        AttackPowerModifier = attackPowerModifier;
        ExtraStatuses = extraStatuses;
    }
}
