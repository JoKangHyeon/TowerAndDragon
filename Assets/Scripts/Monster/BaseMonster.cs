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
[RequireComponent(typeof(MonsterStatusReceiver))]
public class BaseMonster : MonoBehaviour, IAttackTarget, IStatusEffectTarget, IMovementTypedTarget
{
    [SerializeField] private MonsterData _data;

    private Health _health;
    private MonsterShield _shield;
    private MonsterMovement _movement;
    private MonsterAttack _attack;
    private MonsterStatusReceiver _statusReceiver;
    private Castle _mainCastle;
    private Animator _animator;
    private EnemyEnhancementSnapshot _enhancement;
    private float _baseMoveSpeed;
    private readonly SpecialBehaviorRunner _specialBehaviorRunner = new();

    public MonsterData Data => _data;
    public bool IsDead => _health == null || _health.IsDead;

    // 공중/지상 판정의 기준은 붙어 있는 이동 컴포넌트가 아니라 데이터다 - TakeDamage의 속성 면역
    // 판정(_data.AcceptsElement)과 같은 방침이며, 인스턴스화 전에도 읽을 수 있다.
    // 프리팹과 데이터가 어긋난 편성은 WaveDefinitionSO의 검증이 잡는다.
    // WaveManager는 Instantiate 직후 Setup을 호출하므로 그 사이 _data가 비어 있을 수 있다.
    public MonsterMovementType MovementType =>
        _data != null ? _data.MovementType : default;

    // 현재 체력 비례 데미지(스킬 등)를 산정하기 위해 노출한다 - Health 자체는 계속 private로 캡슐화.
    public float CurrentHealth => _health == null ? 0f : _health.CurrentHealth;
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;
    public MonsterAttack Attack => _attack;
    public bool HasArrivedAtCastle => _movement != null && _movement.HasArrived;
    
    public bool HasStatus(string statusId) => 
        _statusReceiver != null && _statusReceiver.HasStatus(statusId);

    public bool IsActionBlocked =>
        _statusReceiver != null &&
        _statusReceiver.IsActionBlocked;

    public bool CanAct =>
        !IsDead &&
        !IsActionBlocked;

    private int _animKeyMove = Animator.StringToHash("Move");
    private int _animKeyTakeDamage = Animator.StringToHash("TakeDamage");


    private void Awake()
    {
        _health = GetComponent<Health>();
        _shield = GetComponent<MonsterShield>();
        _movement = GetComponent<MonsterMovement>();
        _attack = GetComponent<MonsterAttack>();
        _statusReceiver = GetComponent<MonsterStatusReceiver>();
        _animator = GetComponent<Animator>();
    }

    public void ApplyStatus(StatusEffectSO status)
    {
        _statusReceiver?.Apply(status);
    }

    public void ApplyStatus(StatusEffectSO status, DragonType? attackElement)
    {
        if (_data != null && !_data.AcceptsElement(attackElement))
        {
            return;
        }

        _statusReceiver?.Apply(status, attackElement);
    }

    // MonsterStatusReceiver가 슬로우 상태 변화 시 호출한다 - 상태이상 배율은 기준 속도에만 곱한다.
    public void RefreshMoveSpeed()
    {
        if (_movement == null)
        {
            return;
        }

        float multiplier = _statusReceiver != null ? _statusReceiver.MoveSpeedMultiplier : 1f;
        float moveSpeed = _baseMoveSpeed * multiplier;
        _movement.SetSpeed(moveSpeed);
        if (_animator != null)
        {
            _animator.SetBool(_animKeyMove, moveSpeed > 0);
        }
    }

    /// <summary>
    /// 스포너가 호출한다. 데이터 주입 후 컴포넌트를 구성하고 이동을 시작한다.
    /// path는 지상 유닛의 경로, mainCastle은 공중 유닛의 목표이자 도착 지점이다.
    /// </summary>
    public void Setup(MonsterData data, SplineContainer path, Transform mainCastle)
    {
        Setup(data, path, mainCastle, EnemyEnhancementSnapshot.Neutral);
    }

    public void Setup(
        MonsterData data,
        SplineContainer path,
        Transform mainCastle,
        EnemyEnhancementSnapshot enhancement)
    {
        _data = data;
        _enhancement = enhancement;

        _mainCastle = mainCastle != null ? mainCastle.GetComponent<Castle>() : null;

        float maxHealth = Mathf.Max(
            0f,
            _enhancement.MaxHealth.Apply(_data.MaxHealth));
        _health.Initialize(maxHealth);
        _health.Died.AddListener(HandleDeath);

        if (_shield != null && _data.HasShield)
        {
            float shieldAmount = Mathf.Max(
                0f,
                _enhancement.ShieldAmount.Apply(_data.ShieldAmount));
            _shield.Initialize(shieldAmount);
        }

        if (_attack != null && _data.Attack != null)
        {
            _attack.Initialize(_data, _movement, _animator, _enhancement.AttackPower);
        }

        _specialBehaviorRunner.Initialize(this, _data.SpecialBehaviors);

        ConfigureMovement(path, mainCastle);
    }

    private void Update()
    {
        if (!CanAct)
        {
            return;
        }
        _specialBehaviorRunner.Tick(Time.deltaTime);
    }

    public void TakeDamage(DamageInfo damage)
    {
        if (IsDead)
        {
            return;
        }

        if (_data != null && !_data.AcceptsElement(damage.Element))
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
            if(_animator!=null)
                _animator.SetTrigger(_animKeyTakeDamage);
        }
    }

    public void Heal(float amount)
    {
        if (IsDead)
        {
            return;
        }

        _health.Heal(amount);
    }

    public bool GrantShield (float amount)
    {
        if (IsDead || _shield == null || _shield.HasShield)
        {
            return false;
        }

        _shield.Initialize(amount);
        return true;
    }

    public void RevokeGrantedShield()
    {
        _shield?.Clear();
    }

    public void HaltMovement()
    {
        _movement?.Stop();
    }
    
    public void Kill()
    {
        if (IsDead)
        {
            return;
        }
    
        _health.Kill();
    }

    /// <summary>테스트 진행을 위해 방어막과 관계없이 즉시 사망 처리한다.</summary>
    public void DebugDefeatImmediately()
    {
        if (_health == null || _health.IsDead)
        {
            return;
        }

        _health.TakeDamage(_health.CurrentHealth);
    }

    private void ConfigureMovement(SplineContainer path, Transform mainCastle)
    {
        if (_movement == null)
        {
            return;
        }

        _baseMoveSpeed = Mathf.Max(
            0f,
            _enhancement.MoveSpeed.Apply(_data.MoveSpeed));
        RefreshMoveSpeed();

        if (_animator != null)
            _animator.SetBool(_animKeyMove, _movement.GetSpeed() > 0);

        switch (_movement)
        {
            case GroundSplineMovement ground:
                ground.SetPath(path);
                break;
            case AirDirectMovement air:
                air.SetTarget(mainCastle);
                break;
        }

        _movement.Arrived.AddListener(HandleArrivedAtCastle);
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
        _specialBehaviorRunner.Dispose();

        if (_health != null)
        {
            _health.Died.RemoveListener(HandleDeath);
        }

        if (_movement != null)
        {
            _movement.Arrived.RemoveListener(HandleArrivedAtCastle);
        }
    }
}
