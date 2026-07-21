/// <summary>
/// 몬스터 한 마리에 붙는 특수 행동의 런타임 인스턴스.
/// 쿨다운·타이머 등 몬스터별 상태를 보관한다.
/// SpecialBehaviorSO(공유 데이터)가 CreateInstance로 생성한다.
/// </summary>
public interface ISpecialBehavior
{
    /// <summary>
    /// 행동을 소유한 몬스터와 연결한다.
    /// </summary>
    void Initialize(BaseMonster owner);

    /// <summary>
    /// 몬스터가 활성화된 동안 행동을 갱신한다.
    /// </summary>
    void Tick(float deltaTime);

    /// <summary>
    /// 이벤트 구독 등 행동이 보유한 런타임 자원을 정리한다.
    /// </summary>
    void Dispose();
}
