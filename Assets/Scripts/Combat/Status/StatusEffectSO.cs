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

    public string StatusId => _statusId;
    public float DurationSeconds => _durationSeconds;
    public bool IsInfinite => _durationSeconds <= 0f;
}
