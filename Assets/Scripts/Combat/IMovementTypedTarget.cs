/// <summary>
/// 공중/지상 구분을 가진 피격 대상. 대공·대지 전용 공격(TargetMovementFilter)이 이 값을 보고 거른다.
/// 건물처럼 구분이 없는 대상은 구현하지 않으며, 필터는 미구현 대상을 항상 통과시킨다.
/// </summary>
public interface IMovementTypedTarget : IDamageable
{
    MonsterMovementType MovementType { get; }
}
