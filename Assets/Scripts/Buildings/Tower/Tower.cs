using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(TowerAttack))]
public class Tower : Building, IMonsterTarget, IParalyzable, IReviveProgress
{
    [SerializeField] private TowerData _towerData;
    private Health _health;
    private TowerAttack _attack;
    private ITowerStaffing _staffing;
    private TowerAuraSystem _auraSystem;
    private CancellationTokenSource _reviveCts;
    private CancellationTokenSource _paralysisCts;
    protected Animator _animator;

    // 체력이 0이 되어 비활성화된 순간 알린다("파괴 대신 비활성화" - 타워는 사라지지 않는다).
    // 연출(수비병이 튕겨 나오는 그림 등)이 붙는 자리이므로, 구독자가 없어도 게임 규칙은 그대로다.
    // 필드 초기화 시점에 생성해 구독자의 Awake/OnEnable 순서와 무관하게 안전하다
    // (BuildingPlacementController.BuildingSelected와 같은 방식).
    public UnityEvent<Tower> Disabled = new();

    // 비활성화됐던 타워가 다시 가동되는 순간 알린다(부활 대기 완료, 아침 복구 양쪽 모두).
    // 비활성화 동안 띄워 둔 연출을 걷어내는 짝이 되는 이벤트다.
    public UnityEvent<Tower> Reactivated = new();

    private bool _isInitialized;
    private bool _isDisabled;
    private float _disabledAtTime;
    private float _reviveProgress;

    // 감전 효과
    private bool _isParalyzed;
    private float _paralyzedUntil;

    public bool IsDead => _health == null || _health.IsDead;
    public TowerAttack Attack => _attack;
    public TowerData Data => _towerData;
    public TowerAuraSystem AuraSystem => _auraSystem;
    public override IReadOnlyList<ResourceAmount> BuildCost => _towerData != null ? _towerData.BuildCost : base.BuildCost;
    public override int PopulationCapacity => _towerData != null ? _towerData.PopulationCapacity : base.PopulationCapacity;
    
    public virtual MonsterTargetType BaseTargetType => MonsterTargetType.Tower;

    private int _lastAuraCheckFrame = -1;
    private MonsterTargetType _cachedTargetType;

    public MonsterTargetType TargetType
    {
        get
        {
            if (Time.frameCount != _lastAuraCheckFrame)
            {
                _lastAuraCheckFrame = Time.frameCount;
                bool isStealth = false;
                
                if (TowerAuraSystem.TryGetActiveAura(this, out var myAura, out _) && myAura.IsStealth)
                {
                    isStealth = true;
                }
                else if (_auraSystem != null)
                {
                    isStealth = _auraSystem.ResolveModifiers(this).IsStealth;
                }
                
                _cachedTargetType = isStealth ? MonsterTargetType.None : BaseTargetType;
            }
            return _cachedTargetType;
        }
    }

    // 인구로 가동하지 않는 타워(새끼용 등)는 false로 override한다.
    public virtual bool RequiresPopulation => true;

    // 공격 가능 여부에 추가 조건을 거는 서브클래스 훅(새끼용 버프모드 등). 기본은 항상 허용.
    protected virtual bool CanAttackInCurrentMode => true;
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;

    public bool IsParalyzed => _isParalyzed;

    // 체력바 UI가 부활 게이지를 그리는 데 쓴다 - _isDisabled로 판정하므로 별도 상태 추가가 필요 없다.
    public bool IsReviving => _isDisabled;
    public float ReviveProgress =>
        _isDisabled
            ? Mathf.Clamp01(_reviveProgress)
            : 0f;

    protected static readonly int HIT_ANIM_KEY = Animator.StringToHash("Hit");
    protected static readonly int BROKEN_ANIM_KEY = Animator.StringToHash("Broken");

    // 쓰러진 자세(Broken)로 보여야 하는 조건. 기본은 "체력 0으로 비활성화"뿐이고,
    // 새끼용처럼 다른 사유로도 멈추는 타워가 조건을 덧붙인다.
    // 사유가 여럿이면 한쪽이 풀릴 때 다른 쪽 자세까지 지워지므로, 반드시 이 프로퍼티로 합쳐 판정한다.
    protected virtual bool IsBrokenPose => _isDisabled;

    // 파라미터를 직접 켜고 끄지 말고 이 메서드로만 갱신한다(위 합산 판정을 거치게 하려는 것).
    protected void RefreshBrokenAnimation()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.SetBool(BROKEN_ANIM_KEY, IsBrokenPose);
    }


    protected override void Awake()
    {
        base.Awake();
        _health = GetComponent<Health>();
        _attack = GetComponent<TowerAttack>();
        _staffing = GetComponent<ITowerStaffing>();
        _animator = GetComponent<Animator>();
    }

    private void Start()
    {
        if (!_isInitialized && _towerData != null)
        {
            Setup(_towerData);
        }
    }

    public void Setup(TowerData data)
    {
        if (data == null)
        {
            Debug.LogError("[Tower] TowerData가 지정되지 않았습니다.", this);
            return;
        }

        if (_isInitialized)
        {
            _health.Died.RemoveListener(HandleDisabled);
        }
        CancelParalysisRecovery();
        _isParalyzed = false;
        _paralyzedUntil = 0f;


        _towerData = data;
        _health.Initialize(_towerData.MaxHealth);
        _health.Died.AddListener(HandleDisabled);
        _attack.Initialize(_towerData, _animator);
        _isInitialized = true;
        RefreshAttackEnabled();
    }

    // 용 스킬트리의 최대체력 보너스를 다시 적용한다. 공격력·공속과 달리 매 프레임 pull할 수 없는
    // 유일한 스탯이라(Health가 최대치를 값으로 들고 있다) TowerMaxHealthApplier가 밤 시작마다 부른다.
    // 현재 체력은 비율로 보존한다 - 그러지 않으면 최대치가 오를 때마다 만피로 회복돼 버린다.
    public void ApplyMaxHealthMultiplier(float multiplier)
    {
        // 파괴(비활성)된 타워는 건너뛴다 - Initialize가 현재 체력을 최대치로 되돌려
        // 아침 복구를 기다리던 타워가 밤 시작과 함께 되살아나 버린다.
        if (!_isInitialized || _towerData == null || _health == null || IsDead)
        {
            return;
        }

        float ratio = _health.MaxHealth > 0f ? _health.CurrentHealth / _health.MaxHealth : 1f;

        _health.Initialize(_towerData.MaxHealth * multiplier);
        _health.RestoreCurrentHealth(_health.MaxHealth * ratio);
    }

    public void TakeDamage(DamageInfo damage)
    {
        if (IsDead)
        {
            return;
        }

        Debug.Log(
            $"[Tower] {name}이 공격받았습니다. 피해량: {damage.Amount}. 체력 {_health.CurrentHealth}",
            this);

        _health.TakeDamage(damage.Amount);
        if (_animator != null && damage.Amount > 0)
        {
            _animator.SetTrigger(HIT_ANIM_KEY);
        }
    }


    // 타워의 회복
    public void Heal (float amount)
    {
        if (!_isInitialized || IsDead || IsReviving)
        {
            return;
        }

        _health.Heal(amount);
    }

    private void HandleDisabled()
    {
        _isDisabled = true;
        _disabledAtTime = Time.time;
        _reviveProgress = 0f;

        RefreshAttackEnabled();

        Debug.Log(
            $"[Tower] {name}이 비활성화되었습니다. 재활성화 대기시간: {_towerData.ReviveDelay}초",
            this);

        CancelRevive();
        _reviveCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        ReviveAfterDelayAsync(_reviveCts.Token).Forget();

        RefreshBrokenAnimation();

        Disabled.Invoke(this);
    }

    public void RestoreAtMorning()
    {
        if (!_isInitialized)
        {
            return;
        }

        CancelRevive();
        RestoreAndReactivate();
    }

    private async UniTaskVoid ReviveAfterDelayAsync(CancellationToken token)
    {
        while (_reviveProgress < 1f)
        {
            await UniTask.Yield(token);

            float staffingRatio = _staffing != null
                ? Mathf.Clamp01(_staffing.StaffingRatio)
                : 0f;

            if (staffingRatio <= 0f)
            {
                continue;
            }

            if (_towerData.ReviveDelay <= 0f)
            {
                break;
            }

            float reviveSpeedMultiplier = _auraSystem != null
                ? _auraSystem.ResolveModifiers(this).ReviveSpeedMultiplier
                : 1f;

            _reviveProgress +=
                Time.deltaTime * staffingRatio * reviveSpeedMultiplier /
                _towerData.ReviveDelay;
        }

        RestoreAndReactivate();
    }

    public void SetAuraSystem(TowerAuraSystem auraSystem)
    {
        _auraSystem = auraSystem;
    }

    private void CancelRevive()
    {
        if (_reviveCts == null)
        {
            return;
        }

        _reviveCts.Cancel();
        _reviveCts.Dispose();
        _reviveCts = null;
    }

    private void RestoreAndReactivate()
    {
        bool wasDisabled = _isDisabled;
        float disabledDuration = Time.time - _disabledAtTime;

        // IsReviving(=_isDisabled)이 false로 먼저 바뀌어야, RestoreToFull이 쏘는
        // HealthChanged를 체력바가 "만피"로 인식해 즉시 사라진다. 순서가 바뀌면
        // 부활 게이지가 완료 후에도 화면에 남는다.
        _isDisabled = false;
        _reviveProgress = 0f;
        _health.RestoreToFull();

        RefreshAttackEnabled();

        if (!wasDisabled)
        {
            return;
        }

        RefreshBrokenAnimation();

        Debug.Log(
            $"[Tower] {name}이 재활성화되었습니다. 실제 비활성화 시간: {disabledDuration:F2}초",
            this);

        Reactivated.Invoke(this);
    }

    private void OnDestroy()
    {
        CancelRevive();

        CancelParalysisRecovery();

        if (_health != null)
        {
            _health.Died.RemoveListener(HandleDisabled);
        }
    }

    public void ApplyParalysis(float duration)
    {

        if (!_isInitialized || IsDead || duration <= 0f)
        {
            return;
        }

        float requestedEndTime = Time.time + duration;

        if (requestedEndTime <= _paralyzedUntil)
        {
            return;
        }

        _paralyzedUntil = requestedEndTime;
        _isParalyzed = true;


        RefreshAttackEnabled();

        CancelParalysisRecovery();

        _paralysisCts = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        RecoverFromParalysisAfterDelayAsync(_paralysisCts.Token).Forget();
    }

    private async UniTaskVoid RecoverFromParalysisAfterDelayAsync(
        CancellationToken token)
    {
        float remainingDuration = Mathf.Max(
            0f,
            _paralyzedUntil - Time.time
        );

        await UniTask.Delay(
            TimeSpan.FromSeconds(remainingDuration),
            cancellationToken: token
        );

        _paralysisCts?.Dispose();
        _paralysisCts = null;

        _isParalyzed = false;
        _paralyzedUntil = 0f;


        RefreshAttackEnabled();
    }

    private void CancelParalysisRecovery()
    {
        if (_paralysisCts == null)
        {
            return;
        }

        _paralysisCts.Cancel();
        _paralysisCts.Dispose();
        _paralysisCts = null;
    }

    protected void RefreshAttackEnabled()
    {
        bool isAttackEnabled =
            _isInitialized &&
            !_isDisabled &&
            !_isParalyzed &&
            CanAttackInCurrentMode;

        _attack?.SetAttackEnabled(isAttackEnabled);
    }
}
