using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 적의 오케스트레이터. "모든 것을 가진 부모 클래스"가 아니라,
/// 프리팹에 붙은 컴포넌트(Health/Shield/Movement/Attack)를 조립하고
/// 생명주기(주입 → 이동 → 데미지 라우팅 → 사망 → 삭제)만 관리한다.
///
/// 적 종류의 조합(지상/공중 × 공격 × 쉴드 유무)은 상속이 아니라
/// 프리팹에 어떤 컴포넌트를 붙이고 어떤 MonsterData를 주입하느냐로 표현한다.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(MonsterAttack))]
public class BaseMonster : MonoBehaviour, IAttackTarget
{
    [SerializeField] private MonsterData _data;

    private Health _health;
    private MonsterShield _shield;
    private MonsterMovement _movement;
    private MonsterAttack _attack;
    private Castle _mainCastle;

    public MonsterData Data => _data;
    public bool IsDead => _health == null || _health.IsDead;

    // 현재 체력 비례 데미지(스킬 등)를 산정하기 위해 노출한다 - Health 자체는 계속 private로 캡슐화.
    public float CurrentHealth => _health == null ? 0f : _health.CurrentHealth;

    public Transform TargetTransform => transform;

    public GameObject TargetObject => gameObject;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _shield = GetComponent<MonsterShield>();
        _movement = GetComponent<MonsterMovement>();
        _attack = GetComponent<MonsterAttack>();
    }

    /// <summary>
    /// 스포너가 호출한다. 데이터 주입 후 컴포넌트를 구성하고 이동을 시작한다.
    /// path는 지상 유닛의 경로, mainCastle은 공중 유닛의 목표이자 도착 지점이다.
    /// </summary>
    public void Setup(MonsterData data, SplineContainer path, Transform mainCastle)
    {
        _data = data;

        _mainCastle = mainCastle != null ? mainCastle.GetComponent<Castle>() : null;

        _health.Initialize(_data.MaxHealth);
        _health.Died += HandleDeath;

        if (_shield != null && _data.HasShield)
        {
            _shield.Initialize(_data.ShieldAmount);
        }

        if (_attack != null && _data.Attack != null)
        {
            _attack.Initialize(_data, _movement);
        }

        ConfigureMovement(path, mainCastle);
    }

    public void TakeDamage(DamageInfo damage)
    {
        if (IsDead)
        {
            return;
        }

        Debug.Log(
            $"[BaseMonster] {name}이 공격받았습니다. 피해량: {damage.Amount}. 체력 : {_health.CurrentHealth}",
            this);

        float remaining = damage.Amount;

        if (_shield != null && !_shield.IsBroken)
        {
            remaining = _shield.Absorb(remaining);
        }

        if (remaining > 0)
        {
            _health.TakeDamage(remaining);
        }
    }

    private void ConfigureMovement(SplineContainer path, Transform mainCastle)
    {
        if (_movement == null)
        {
            return;
        }

        _movement.SetSpeed(_data.MoveSpeed);

        switch (_movement)
        {
            case GroundSplineMovement ground:
                ground.SetPath(path);
                break;
            case AirDirectMovement air:
                air.SetTarget(mainCastle);
                break;
        }

        _movement.Arrived += HandleArrivedAtCastle;
        _movement.Begin();
    }

    private void HandleArrivedAtCastle()
    {
        if (_attack == null || _mainCastle == null)
        {
            return;
        }

        _attack.SetFinalTarget(_mainCastle);
        // 성에 도달했을 때의 처리(성 공격)를 연결하는 지점.
        // 밤 방어 로직과 함께 구현 예정 — 현재 작업 범위 밖.
    }

    private void HandleDeath()
    {
        // 사망 시 효과(슬라임/자원 드랍, 이펙트)를 연결하는 지점.
        // 오브젝트 풀링은 보류 상태이므로 지금은 파괴로 정리한다.
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDeath;
        }

        if (_movement != null)
        {
            _movement.Arrived -= HandleArrivedAtCastle;
        }
    }
}
