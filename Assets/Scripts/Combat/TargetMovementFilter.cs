/// <summary>
/// 하나의 공격이 노릴 수 있는 대상의 이동 방식. "대공 전용 / 대지 전용" 타워를 데이터로 표현한다.
///
/// [Flags]가 아닌 이유:
/// - 기본값 0이 All이라 이 필드가 없던 기존 애셋(YAML에 키 자체가 없어 0으로 역직렬화된다)이
///   마이그레이션 없이 종전과 동일하게 동작한다.
/// - [Flags]로 만들면 0이 인스펙터에 "Nothing"으로 표시되어 기획자에게 정반대 의미로 읽힌다.
///   일반 enum이면 "All"로 그대로 표시된다.
/// 지금 필요한 경우가 3가지뿐이라 조합은 지원하지 않는다.
/// </summary>
public enum TargetMovementFilter
{
    All = 0,
    GroundOnly,
    AirOnly,
}

public static class TargetMovementFilterExtensions
{
    public static bool Allows(this TargetMovementFilter filter, MonsterMovementType movementType)
    {
        return filter switch
        {
            TargetMovementFilter.All => true,

            TargetMovementFilter.GroundOnly =>
                movementType == MonsterMovementType.Ground,

            TargetMovementFilter.AirOnly =>
                movementType == MonsterMovementType.Air,

            _ => true,
        };
    }

    /// <summary>
    /// 공중/지상 구분이 없는 대상(타워·성 등 IMovementTypedTarget 미구현)은 항상 통과시킨다.
    /// 몬스터가 같은 AttackSO를 공유하더라도 필터 때문에 조용히 무력화되지 않도록 하기 위함이다.
    /// </summary>
    public static bool CanTarget(this TargetMovementFilter filter, IDamageable target)
    {
        if (target is not IMovementTypedTarget movementTypedTarget)
        {
            return true;
        }

        return filter.Allows(movementTypedTarget.MovementType);
    }
}
