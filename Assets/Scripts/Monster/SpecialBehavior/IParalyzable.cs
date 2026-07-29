/// <summary>
/// 일정 시간 동안 감전될 수 있는 대상의 계약.
/// 감전 중첩과 해제 시점은 구현체가 관리한다.
/// </summary>
public interface IParalyzable
{
    bool IsParalyzed { get; }

    void ApplyParalysis(float duration);
}