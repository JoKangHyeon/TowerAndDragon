using UnityEngine;

// 몬스터에 부여되는 지속 효과(슬로우·화상·빙결 등)의 데이터 베이스.
// IAttackEffect.cs가 "상태 시스템은 [미정] - 팀 확정 후 도입"이라 남겨둔 지점을 채운다.
// 수치는 전부 파생 클래스의 [SerializeField]에 둔다 - 밸런싱 변경 시 코드 수정이 없어야 한다.
public abstract class StatusEffectSO : ScriptableObject
{
    // 같은 Id를 가진 상태끼리만 "동일 상태의 갱신"으로 취급한다(MonsterStatusReceiver 참고).
    [SerializeField] private string _statusId;

    // 0 이하 = 무한 지속(별도 해제 시점까지 유지). 불 패시브(상시 화상)처럼 발동 조건이
    // 사라질 때까지 계속돼야 하는 상태에 쓴다.
    [SerializeField] private float _durationSeconds;

    // 군중제어 면역(보스)을 무시하고 그래도 걸리는 상태인지. 어미용의 액티브 전역 빙결처럼
    // 쿨다운으로 제한된 소수의 "한 방"에만 켠다 - 상시 부여되는 타워·새끼용 상태에 켜면
    // 보스의 군중제어 면역이 사실상 사라진다.
    // 스택 상태(StackingStatusEffectSO)로 발동시키는 경우, 임계치에서 부여할 상태만이 아니라
    // 스택 상태 자체에도 켜야 한다 - 스택을 쌓는 단계에서 먼저 걸러지기 때문이다.
    [Tooltip("보스 등 군중제어 면역 대상에게도 적용된다. 쿨다운으로 제한된 액티브 스킬에만 켠다.")]
    [SerializeField] private bool _piercesCrowdControlImmunity;

    public string StatusId => _statusId;
    public float DurationSeconds => _durationSeconds;
    public bool IsInfinite => _durationSeconds <= 0f;
    public bool PiercesCrowdControlImmunity => _piercesCrowdControlImmunity;

    // 군중제어(둔화·빙결·경직 등 이동/행동을 제약하는 효과)인지 여부.
    // 보스처럼 군중제어 면역인 몬스터가 "무엇을 걸러낼지"를 상태 쪽에서 선언하게 해,
    // 새 군중제어 상태를 추가할 때 면역 판정부(MonsterStatusReceiver)를 고치지 않아도 되게 한다.
    // 지속피해(화상 등)는 군중제어가 아니므로 기본값 false를 그대로 쓴다.
    public virtual bool IsCrowdControl => false;
}
