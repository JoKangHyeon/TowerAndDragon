using UnityEngine;

/// <summary>
/// 특수 행동의 공유 데이터(설정값)이자 런타임 인스턴스 팩토리.
/// AttackEffectSO와 달리 행동은 몬스터별 상태를 가지므로,
/// SO는 설정만 담고 CreateInstance로 상태를 가진 인스턴스를 만든다.
/// 실제 애셋 생성 메뉴는 이 클래스를 상속하는 구체 행동 SO에 선언한다.
/// </summary>
public abstract class SpecialBehaviorSO : ScriptableObject
{
    /// <summary>
    /// 이 설정을 사용하는 몬스터 한 마리의 런타임 행동을 생성한다.
    /// 반환 인스턴스는 다른 몬스터와 공유하지 않는다.
    /// </summary>
    public abstract ISpecialBehavior CreateInstance();
}
