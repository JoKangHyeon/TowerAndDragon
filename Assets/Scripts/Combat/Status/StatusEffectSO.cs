using UnityEngine;

// 몬스터에 부여되는 지속 효과(슬로우·화상·빙결 등)의 데이터 베이스.
// IAttackEffect.cs가 "상태 시스템은 [미정] - 팀 확정 후 도입"이라 남겨둔 지점을 채운다.
// 수치는 전부 파생 클래스의 [SerializeField]에 둔다 - 밸런싱 변경 시 코드 수정이 없어야 한다.
public abstract class StatusEffectSO : ScriptableObject
{
    // 같은 Id를 가진 상태끼리만 "동일 상태의 갱신"으로 취급한다(MonsterStatusReceiver 참고).
    [SerializeField] private string _statusId;

    // 툴팁에 적을 이름의 스트링테이블 키. StatusId와 따로 두는 이유는 Id가 출처별로 갈리기 때문이다 -
    // tower_ice_slow / baby_ice_slow / dragon_ice_slow는 화면에 전부 "둔화"로 나와야 한다.
    // 비워 두면 그 상태는 툴팁에 줄을 만들지 않는다 - 키를 안 채운 애셋이 빈 줄이나
    // 키 문자열을 그대로 노출하는 것보다 조용히 빠지는 편이 낫다.
    [Tooltip("툴팁에 표시할 이름의 스트링테이블 키. 비우면 툴팁에 나오지 않는다.")]
    [SerializeField] private string _displayNameLocKey;

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

    // 상태가 걸려 있는 동안 대상 몬스터의 몸통에 띄울 연출. 상태 데이터가 자기 연출을 들고 있으므로
    // 새 상태를 추가할 때 어딘가의 배선표를 늘리지 않아도 된다(MonsterStatusVfx 참고).
    //
    // 명중 이펙트와 역할이 갈린다 - 명중은 1회 사건이라 지속 구간을 못 그리고, 상태가 안 걸린
    // 대상(속성 면역·군중제어 면역)에도 재생돼 거짓 신호가 된다. 둘은 보완 관계다.
    // 비워 두는 것이 기본이다 - 화면에 상태 걸린 몬스터가 여럿일 때 전투를 가리지 않아야 한다.
    [WiringOptional]
    [Tooltip("상태가 걸려 있는 동안 몬스터 몸통에 띄울 VFX 프리팹. 비우면 표시하지 않는다.")]
    [SerializeField] private GameObject _activeVfxPrefab;

    // 몸통 중앙(스프라이트 경계의 중심) 기준 보정. 스프라이트마다 실제 몸통이 경계 중심에서
    // 조금씩 벗어나므로, 눈으로 맞추는 값을 코드가 아니라 애셋에 둔다.
    [Tooltip("몸통 중앙 기준 추가 보정(월드 단위).")]
    [SerializeField] private Vector2 _activeVfxOffset;

    public string StatusId => _statusId;
    public string DisplayNameLocKey => _displayNameLocKey;
    public bool HasDisplayName => !string.IsNullOrEmpty(_displayNameLocKey);
    public float DurationSeconds => _durationSeconds;
    public bool IsInfinite => _durationSeconds <= 0f;
    public bool PiercesCrowdControlImmunity => _piercesCrowdControlImmunity;
    public GameObject ActiveVfxPrefab => _activeVfxPrefab;
    public bool HasActiveVfx => _activeVfxPrefab != null;
    public Vector2 ActiveVfxOffset => _activeVfxOffset;

    // 군중제어(둔화·빙결·경직 등 이동/행동을 제약하는 효과)인지 여부.
    // 보스처럼 군중제어 면역인 몬스터가 "무엇을 걸러낼지"를 상태 쪽에서 선언하게 해,
    // 새 군중제어 상태를 추가할 때 면역 판정부(MonsterStatusReceiver)를 고치지 않아도 되게 한다.
    // 지속피해(화상 등)는 군중제어가 아니므로 기본값 false를 그대로 쓴다.
    public virtual bool IsCrowdControl => false;

#if UNITY_EDITOR
    // 잘못 꽂은 프리팹은 런타임에 조용히 아무 것도 안 보이는 것으로 나타난다 - "안 보이는데
    // 왜인지 모르겠다"가 되기 전에 인스펙터에서 잡는다.
    protected virtual void OnValidate()
    {
        if (_activeVfxPrefab == null)
        {
            return;
        }

        if (_activeVfxPrefab.GetComponentInChildren<ParticleSystem>(true) == null)
        {
            Debug.LogWarning(
                $"[{name}] ActiveVfxPrefab '{_activeVfxPrefab.name}'에 ParticleSystem이 없습니다. " +
                "상태 지속 연출은 파티클 프리팹을 전제로 합니다.",
                this);
        }
    }
#endif
}
