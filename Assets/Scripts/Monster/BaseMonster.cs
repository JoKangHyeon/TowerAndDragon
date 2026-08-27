using System.Collections.Generic;
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

    private static readonly List<BaseMonster> ACTIVE_MONSTERS = new();

    // 씬에 살아 있는(파괴되지 않은) 적 전체. MonsterPicker의 몸통 우선 판정이 이 목록만 인덱서로
    // 훑는다 - WaveManager.SpawnedMonsters는 웨이브가 끝나야 정리되고(AreAllMonstersDefeated 안),
    // 디버그 스포너가 만든 개체는 아예 들어가지 않아 "지금 살아 있는 적"의 목록으로 쓸 수 없다.
    public static IReadOnlyList<BaseMonster> ActiveMonsters => ACTIVE_MONSTERS;

    // 플레이모드 재진입 시 Reload Domain이 꺼져 있으면 이전 세션의 파괴된 항목이 목록에 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => ACTIVE_MONSTERS.Clear();

    // 안개 판정에 쓰는 GridMap - 씬당 하나뿐이므로 정적으로 공유한다. 없는 씬(팀원 테스트 씬 등)에서는
    // null로 남아 IsFogHidden이 항상 false를 돌려준다.
    private static GridMap _gridMap;

    private Health _health;
    private MonsterShield _shield;
    private MonsterMovement _movement;
    private MonsterAttack _attack;
    private MonsterStatusReceiver _statusReceiver;
    private Castle _mainCastle;
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private EnemyEnhancementSnapshot _enhancement;
    private float _baseMoveSpeed;
    private readonly SpecialBehaviorRunner _specialBehaviorRunner = new();

    public MonsterData Data => _data;
    public bool IsDead => _health == null || _health.IsDead;

    // 이 적에 마지막으로 적용된 화면 정렬 순서(IsometricDepthSorter가 매 프레임 쓴 실제 값).
    // 건물의 Building.DepthSortOrder와 같은 눈금(IsometricMath.ComputeDepthSortOrder)이라,
    // 호버 판정이 적과 건물을 이 값 하나로 비교해 화면 앞쪽을 고를 수 있다.
    public int DepthSortOrder => _spriteRenderer != null ? _spriteRenderer.sortingOrder : 0;

    /// <summary>월드 좌표가 이 적의 스프라이트 몸통 안인지. 건물의 Building.ContainsWorldPoint와
    /// 같은 판정(SpriteHitTest)을 쓴다 - 콜라이더가 발밑에 작게 있어 큰 스프라이트(보스 등)는
    /// 콜라이더만으로 커서로 집기 어렵다.</summary>
    public bool ContainsWorldPoint(Vector3 worldPoint) =>
        SpriteHitTest.Contains(_spriteRenderer, worldPoint);

    /// <summary>안개(Hidden 청크) 위에 서 있는가. 호버 판정에서 걸러내는 데 쓴다 - 호버 아웃라인은
    /// 전체화면 패스(EPO Outliner, AfterTransparents)로 안개 구름(Fog 정렬 레이어) 위에 그려지므로,
    /// 걸러내지 않으면 정찰하지 않은 적의 실루엣이 구름을 뚫고 그대로 드러난다.</summary>
    public bool IsFogHidden
    {
        get
        {
            if (_gridMap == null)
            {
                _gridMap = FindFirstObjectByType<GridMap>();
            }

            if (_gridMap == null)
            {
                return false;
            }

            Vector3Int coord = _movement != null
                ? _gridMap.ConvertWorldToGrid(_movement.GroundPlanePosition)
                : _gridMap.PickCellAtWorldPoint(transform.position);

            return _gridMap.GetCellState(coord) == ChunkState.Hidden;
        }
    }

    // 공중/지상 판정의 기준은 붙어 있는 이동 컴포넌트가 아니라 데이터다 - TakeDamage의 속성 면역
    // 판정(_data.AcceptsElement)과 같은 방침이며, 인스턴스화 전에도 읽을 수 있다.
    // 프리팹과 데이터가 어긋난 편성은 WaveDefinitionSO의 검증이 잡는다.
    // WaveManager는 Instantiate 직후 Setup을 호출하므로 그 사이 _data가 비어 있을 수 있다.
    public MonsterMovementType MovementType =>
        _data != null ? _data.MovementType : default;

    // 현재 체력 비례 데미지(스킬 등)를 산정하기 위해 노출한다 - Health 자체는 계속 private로 캡슐화.
    public float CurrentHealth => _health == null ? 0f : _health.CurrentHealth;

    // 툴팁이 "42 / 120"을 적으려면 최대 체력도 필요하다. MonsterData.MaxHealth로는 안 된다 -
    // Setup에서 강화 배율이 곱해지므로 설계 원본값과 실제 개체의 최대 체력이 다르다.
    public float MaxHealth => _health == null ? 0f : _health.MaxHealth;

    // 방어막은 표시용 계약만 넘긴다 - 툴팁이 MonsterShield의 Absorb/Clear까지 만질 이유가 없다.
    public IShieldInfo Shield => _shield;

    // 이 개체에 적용된 전역 강화. 낮의 출현 예고 카드가 밤의 실제 수치와 같은 값을 보여주는 데 쓴다.
    public EnemyEnhancementSnapshot Enhancement => _enhancement;

    /// <summary>지금 걸린 상태이상을 툴팁용 줄로 buffer에 덧붙인다(수신기는 계속 private로 캡슐화).</summary>
    public void CollectActiveStatuses(System.Collections.Generic.List<MonsterStatusLine> buffer) =>
        _statusReceiver?.CollectActiveStatuses(buffer);
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;
    public MonsterAttack Attack => _attack;
    public bool HasArrivedAtCastle => _movement != null && _movement.HasArrived;
    
    public bool HasStatus(string statusId) => 
        _statusReceiver != null && _statusReceiver.HasStatus(statusId);

    // 둔화·빙결 등 군중제어 상태를 아예 받지 않는다(보스). 속성 면역과 같이 데이터로 결정한다.
    public bool IsCrowdControlImmune =>
        _data != null &&
        _data.IsCrowdControlImmune;

    public bool IsActionBlocked =>
        _statusReceiver != null &&
        _statusReceiver.IsActionBlocked;

    public bool CanAct =>
        !IsDead &&
        !IsActionBlocked;

    private int _animKeyMove = Animator.StringToHash("Move");
    private int _animKeyTakeDamage = Animator.StringToHash("TakeDamage");
    private int _animKeyDied = Animator.StringToHash("Died");

    // 컨트롤러에 Move 파라미터가 있는지. Move 반영을 매 프레임 하므로 없는 컨트롤러
    // (아트 미배정 프리팹에 꽂힌 플레이스홀더)에서는 프레임마다 "Parameter does not exist"
    // 경고가 쌓인다. 해시 오버로드도 경고하므로 한 번 확인해 캐시한다.
    private bool _hasMoveParameter;


    private void Awake()
    {
        _health = GetComponent<Health>();
        _shield = GetComponent<MonsterShield>();
        _movement = GetComponent<MonsterMovement>();
        _attack = GetComponent<MonsterAttack>();
        _statusReceiver = GetComponent<MonsterStatusReceiver>();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _hasMoveParameter = AnimatorParameterUtility.Has(_animator, _animKeyMove);
    }

    // 등록은 OnEnable에서 한다 - MonsterPicker의 몸통 우선 판정이 이 목록을 인덱서로 훑는다.
    private void OnEnable() => ACTIVE_MONSTERS.Add(this);

    private void OnDisable() => ACTIVE_MONSTERS.Remove(this);

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
        _movement.SetSpeed(_baseMoveSpeed * multiplier);
        ApplyMovementAnimation();
    }

    /// <summary>
    /// 애니메이터의 Move bool과 재생 속도를 현재 이동 상태에 맞춘다.
    /// </summary>
    /// <remarks>
    /// Move 판정은 "속도 > 0"이 아니라 MonsterMovement.IsAdvancing이어야 한다 - 공격하려고 멈출 때
    /// Stop()은 _isMoving만 내리고 속도는 유지하므로, 속도 기준으로는 Move가 스폰 이후 계속 true로
    /// 남는다. 그러면 애니메이터가 Move 상태를 벗어나지 못하고, Attack의 유일한 진입로인
    /// Idle -> Attack에 도달할 수 없어 공격 모션이 아예 재생되지 않는다.
    /// </remarks>
    private void ApplyMovementAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        if (_hasMoveParameter)
        {
            _animator.SetBool(_animKeyMove, _movement != null && _movement.IsAdvancing);
        }

        // 빙결은 애니메이션까지 멈춰야 "얼어붙었다"로 읽힌다 - Move를 꺼도 대기 동작은 계속 돈다.
        _animator.speed = IsActionBlocked ? 0f : 1f;
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
        // 매 프레임 다시 반영한다. 이동/정지 전환은 공격을 시작하는 "한 번"만 일어나므로 그 시점에
        // 애니메이터가 준비돼 있지 않으면 공격 내내 모션이 죽는다(같은 이유와 대처가
        // Villager.SetLocomotionState 주석에 정리돼 있다). Stop()/Begin()을 부르는 지점이
        // MonsterAttack·SpecialBehavior 등 여러 곳으로 흩어져 있어, 호출부마다 애니메이터를
        // 갱신하게 만드는 대신 상태를 여기서 한 번에 따라가게 한다.
        // CanAct 검사보다 앞에 둔다 - 행동이 막힌 동안에도 재생 속도는 갱신돼야 한다.
        ApplyMovementAnimation();

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

        // Begin() 뒤에 한 번 더 반영한다 - 위의 RefreshMoveSpeed 시점에는 아직 이동을 시작하지
        // 않아 IsAdvancing이 false이므로, 그대로 두면 스폰 직후 한 프레임 동안 Move가 false다.
        ApplyMovementAnimation();
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
        HaltMovement(); // 사망 시 이동 즉시 정지

        if (_animator != null)
        {
            _animator.speed = 1f; // 빙결 등 상태이상으로 멈춰있을 수 있으므로 정상 속도로 복구
            _animator.SetBool(_animKeyDied, true);
            
            // 시체가 타겟팅되거나 충돌하지 않도록 콜라이더 비활성화
            Collider2D col = GetComponent<Collider2D>();
            if (col != null) col.enabled = false;

            // 2D 스프라이트 애니메이션이므로 애니메이션 재생 시간 대기 후 파괴
            Destroy(gameObject, 1.0f); 
        }
        else
        {
            Destroy(gameObject);
        }
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
