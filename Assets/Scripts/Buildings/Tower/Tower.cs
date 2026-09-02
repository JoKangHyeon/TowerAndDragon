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
    private ITowerCombatRepairUnlockQuery _combatRepairUnlockQuery;
    private ITowerReviveGateQuery _reviveGateQuery;
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

    // 이번 밤에 몇 번 다시 가동됐는가. 밤이 시작될 때마다 TowerMorningRestoreSystem이 0으로 되돌린다.
    // 아침 복구 시점에 되돌리지 않는 이유: RestoreAll은 긴 밤(no_morning_restore) 뮤테이터에서
    // 통째로 건너뛰어지므로, 그 런에서는 첫 밤에 예산을 쓴 타워가 런이 끝날 때까지 영구히 불능이 된다.
    //
    // 세이브에 넣지 않는다. 저장은 낮에만 가능하고(SaveService.IsSaveablePhase가 CycleState.Day를 본다)
    // 예산은 밤 시작마다 0으로 초기화되므로, 저장하든 안 하든 다음 밤의 시작 상태가 똑같다.
    private int _nightRevivesUsed;

    // 세이브 복원이 맡긴 체력·부활 진행도. Setup 이전에 도착하는 경우가 있어 값을 받아 두고
    // Setup 끝에서 적용한다(RestoreHealth 주석 참고).
    private HealthRestoreRequest? _pendingHealthRestore;

    // 감전 효과
    private bool _isParalyzed;
    private float _paralyzedUntil;

    public bool IsDead => _health == null || _health.IsDead;

    /// <summary>세이브 캡처가 읽는 현재 체력. 초기화 전이면 0(= 기록 없음)이다.</summary>
    public float CurrentHealth => _health != null ? _health.CurrentHealth : 0f;

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

    // 인구로 가동하지 않는 타워(새끼용, 영혼타워 등)는 false를 반환한다.
    public virtual bool RequiresPopulation => GetComponent<TowerPopulation>() != null;

    // 공격 가능 여부에 추가 조건을 거는 서브클래스 훅(새끼용 버프모드 등). 기본은 항상 허용.
    protected virtual bool CanAttackInCurrentMode => true;
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;

    public bool IsParalyzed => _isParalyzed;

    // 체력바 UI가 부활 게이지를 그리는 데 쓴다 - _isDisabled로 판정하므로 별도 상태 추가가 필요 없다.
    //
    // <b>"이번 밤에 다시 가동될 수 있는가"를 여기에 섞지 않는다.</b> SaveCapture가 이 값을 그대로
    // BuildingPlacementDto.IsDisabled로 저장하는데, 예산이 바닥난 타워를 여기서 false로 만들면
    // "IsDisabled=false인데 CurrentHealth=0"으로 저장되고, 그 조합은 RestoreHealth가 "기록 없음"으로
    // 읽어 만피·활성으로 되살린다. 게이지가 실제로 도는지는 IsReviveInProgress로 묻는다.
    public bool IsReviving => _isDisabled;

    /// <summary>부활 게이지가 실제로 차오르는 중인가. <see cref="IsReviving"/>과 달리 이번 밤의
    /// 재활성화 예산까지 본다. "부서져 있는가"를 묻는 곳(세이브 캡처·회복 차단·연출 정리)은
    /// 반드시 <see cref="IsReviving"/> 쪽을 쓴다.</summary>
    public bool IsReviveInProgress => _isDisabled && HasNightReviveBudget;

    public float ReviveProgress =>
        _isDisabled
            ? Mathf.Clamp01(_reviveProgress)
            : 0f;

    // 이번 밤에 다시 가동될 여지가 남았는가. 데이터가 없으면 판정할 수 없으므로 막는다
    // (Setup 이전에는 어차피 부활 경로가 전부 _isInitialized에서 걸린다).
    private bool HasNightReviveBudget =>
        _towerData != null && _nightRevivesUsed < _towerData.MaxNightRevives;

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

        // Initialize가 만피로 세팅한 체력을 세이브값으로 되돌린다 - Castle이 Start 뒤에
        // RestoreHealth로 덮어쓰는 것과 같은 순서다.
        ApplyPendingHealthRestore();
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

    public virtual void TakeDamage(DamageInfo damage)
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
            HasNightReviveBudget
                ? $"[Tower] {name}이 비활성화되었습니다. 재활성화 대기시간: {_towerData.ReviveDelay}초"
                    + $" (이번 밤 사용: {_nightRevivesUsed}/{_towerData.MaxNightRevives})"
                : $"[Tower] {name}이 비활성화되었습니다. 이번 밤의 재활성화 횟수"
                    + $"({_towerData.MaxNightRevives}회)를 모두 써서 스스로는 다시 서지 않습니다"
                    + " (시간 어미용 스킬로는 복구 가능).",
            this);

        TryStartCombatRevive();

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
        RestoreAndReactivate(ReactivationCause.MorningRestore);
    }

    /// <summary>밤이 시작될 때 재활성화 예산을 되돌린다(<see cref="TowerMorningRestoreSystem"/>이 부른다).
    ///
    /// <b>예산만 되돌리면 안 된다.</b> 예산이 없어 시작되지 않았던 부활 루프를 여기서 다시 띄운다 -
    /// <see cref="TryStartCombatRevive"/>를 부르는 곳은 <see cref="HandleDisabled"/>와
    /// <see cref="SetCombatRepairUnlockQuery"/>뿐이라, 이미 부서진 채 밤을 맞은 타워는
    /// 아무도 다시 시작해 주지 않아 예산이 있어도 영영 게이지가 돌지 않는다.</summary>
    public void ResetNightReviveBudget()
    {
        _nightRevivesUsed = 0;

        if (_isDisabled)
        {
            TryStartCombatRevive();
        }
    }

    /// <summary>세이브 복원 전용. Setup의 Initialize가 만피·활성으로 세팅한 상태를 저장값으로 되돌린다.
    /// (<see cref="Castle.RestoreHealth"/>와 같은 자리·같은 역할이다.)
    ///
    /// <b>Setup보다 먼저 불릴 수 있다.</b> SaveRestore는 GridMap.RestoreBuilding으로 방금 Instantiate한
    /// 인스턴스에 쓰는데, 평범한 타워의 Setup은 다음 프레임의 Tower.Start가 스스로 부른다.
    /// 그래서 값을 받아 두고 Setup 끝에서 적용한다 - 호출부가 프레임 순서를 알 필요가 없다.</summary>
    /// <param name="currentHealth">저장 당시 현재 체력. isDisabled가 false인데 0 이하면 "기록 없음"으로
    /// 보고 만피를 유지한다(구버전 세이브·타워가 아닌 건물).</param>
    /// <param name="isDisabled">저장 당시 비활성 상태였는가.</param>
    /// <param name="reviveProgress">부활 게이지 진행도(0~1). isDisabled일 때만 쓴다.</param>
    public void RestoreHealth(float currentHealth, bool isDisabled, float reviveProgress)
    {
        _pendingHealthRestore = new HealthRestoreRequest(currentHealth, isDisabled, reviveProgress);

        if (_isInitialized)
        {
            ApplyPendingHealthRestore();
        }
    }

    // 새 복구·파괴 경로를 만들지 않는다 - 비활성은 기존 사망 경로(Health.Died -> HandleDisabled)를
    // 그대로 타야 애니메이션·Disabled 이벤트·전투 수리 시작이 빠지지 않는다.
    private void ApplyPendingHealthRestore()
    {
        if (_pendingHealthRestore == null)
        {
            return;
        }

        HealthRestoreRequest request = _pendingHealthRestore.Value;
        _pendingHealthRestore = null;

        if (request.IsDisabled)
        {
            // Health.RestoreCurrentHealth는 1 미만으로 내려가지 않아(로드 직후 게임오버 방지)
            // 비활성 상태를 만들 수 없다. Died를 발화하는 경로는 Kill뿐이다.
            _health.Kill();

            // HandleDisabled가 진행도를 0으로 초기화하므로 반드시 그 뒤에 세운다 -
            // 순서가 바뀌면 밤새 차오른 게이지가 매번 0부터 다시 시작한다.
            _reviveProgress = Mathf.Clamp01(request.ReviveProgress);
            return;
        }

        if (request.CurrentHealth <= 0f)
        {
            return;
        }

        _health.RestoreCurrentHealth(request.CurrentHealth);
    }

    // 세이브에서 온 체력 3값 묶음. 세 필드를 따로 들면 "적용 대기 중인가"를 판정할 플래그가 하나 더
    // 필요해지므로 nullable 하나로 묶는다.
    private readonly struct HealthRestoreRequest
    {
        public readonly float CurrentHealth;
        public readonly bool IsDisabled;
        public readonly float ReviveProgress;

        public HealthRestoreRequest(float currentHealth, bool isDisabled, float reviveProgress)
        {
            CurrentHealth = currentHealth;
            IsDisabled = isDisabled;
            ReviveProgress = reviveProgress;
        }
    }

    /// <summary>시간 어미용 액티브 스킬(RepairTowersSkill)의 즉시 수리. 이 스킬의 유일한 진입점이다.
    ///
    /// <b>하룻밤 재활성화 횟수(<see cref="TowerData.MaxNightRevives"/>)의 적용을 받지 않는다.</b>
    /// 예산이 바닥난 타워도 이 경로로는 다시 세울 수 있고, 세운다고 예산이 깎이지도 않는다 -
    /// 스킬은 자기 쿨다운으로 이미 제한되며, 그 제한을 뚫는 것이 시간 어미용의 값어치다.
    ///
    /// <b><see cref="CanUseCombatRepair"/>(편의 연구 해금)도 따르지 않는다.</b> 그 연구가 잠그는 것은
    /// 타워가 스스로 일어서는 자연 부활(<see cref="TryStartCombatRevive"/>)뿐이다 - 어미용 스킬은
    /// 이미 자기 쿨다운으로 제한되므로 저티어 편의 연구에 다시 종속시킬 이유가 없다.</summary>
    public bool TryRestoreDuringCombat()
    {
        if (!_isInitialized || !IsDead)
        {
            return false;
        }

        CancelRevive();
        RestoreAndReactivate(ReactivationCause.SkillRepair);
        return true;
    }

    private async UniTaskVoid ReviveAfterDelayAsync(CancellationToken token)
    {
        while (_reviveProgress < 1f)
        {
            await UniTask.Yield(token);

            // 관문이 닫혀 있는 동안에는 진행도를 올리지 않는다(진행도는 보존한다).
            // no_morning_restore가 켜진 런에서 "낮에는 게이지가 멈춘다"가 여기서 성립한다 -
            // 아침 복구만 막으면 낮에도 게이지가 차올라 결국 부활해 버려 뮤테이터가 무효가 된다.
            // 관문이 주입되지 않았으면(미배선·튜토리얼·뮤테이터 없음) 조건이 단락돼 기존 루프와 완전히 같다.
            if (_reviveGateQuery != null && !_reviveGateQuery.CanAdvanceRevive)
            {
                continue;
            }

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

        RestoreAndReactivate(ReactivationCause.NightRevive);
    }

    public void SetAuraSystem(TowerAuraSystem auraSystem)
    {
        _auraSystem = auraSystem;
    }

    /// <summary>부활 게이지 관문을 주입한다. null을 넣으면 관문 없음(항상 진행)으로 돌아간다 -
    /// TowerStatMultiplierCoordinator가 오라 시스템을 뗄 때 쓰는 해제 규약과 같다.</summary>
    public void SetReviveGateQuery(ITowerReviveGateQuery reviveGateQuery)
    {
        _reviveGateQuery = reviveGateQuery;
    }

    public void SetCombatRepairUnlockQuery(ITowerCombatRepairUnlockQuery combatRepairUnlockQuery)
    {
        _combatRepairUnlockQuery = combatRepairUnlockQuery;

        if (_isDisabled)
        {
            TryStartCombatRevive();
        }
    }

    private bool CanUseCombatRepair =>
        _combatRepairUnlockQuery != null &&
        _combatRepairUnlockQuery.IsTowerCombatRepairUnlocked;

    private void TryStartCombatRevive()
    {
        if (!CanUseCombatRepair || !HasNightReviveBudget || _reviveCts != null)
        {
            return;
        }

        _reviveCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        ReviveAfterDelayAsync(_reviveCts.Token).Forget();
    }

    private void CancelRevive()
    {
        if (_reviveCts == null)
        {
            return;
        }

        _reviveCts.Cancel();
        DisposeReviveCts();
    }

    // 취소는 하지 않고 정리만 한다. ReviveAfterDelayAsync가 자연 완료로 여기에 들어온 경우
    // 자기 토큰을 취소할 이유가 없기 때문이다(루프는 이미 빠져나왔다).
    private void DisposeReviveCts()
    {
        if (_reviveCts == null)
        {
            return;
        }

        _reviveCts.Dispose();
        _reviveCts = null;
    }

    // 재활성화가 어느 경로로 일어났는가. 이번 밤의 예산을 소모하는지가 여기서 갈린다.
    // 호출부에서 카운터를 올리지 않고 합류점 하나에서만 올리려고 두는 값이다 -
    // 네 번째 복구 경로가 생겨도 인자를 고르는 순간 예산 처리를 함께 정하게 된다.
    private enum ReactivationCause
    {
        // 부활 게이지 완주. 이번 밤의 예산을 소모하는 유일한 경로다.
        NightRevive,

        // 시간 어미용 액티브 스킬의 즉시 수리. 예산에 걸리지도, 예산을 깎지도 않는다
        // (TryRestoreDuringCombat 주석 참고).
        SkillRepair,

        // 아침 일괄 복구. 밤 예산과 무관하다.
        MorningRestore,
    }

    private void RestoreAndReactivate(ReactivationCause cause)
    {
        // 자연 완료로 부활한 경우에도 CTS를 반드시 비운다. 남겨 두면 TryStartCombatRevive의
        // "_reviveCts != null" 가드에 걸려 그 타워는 두 번째 자동 부활을 하지 못하고,
        // 아침 복구·전투 수리가 CancelRevive로 비워 줄 때까지 부서진 채로 남는다.
        // RestoreAtMorning / TryRestoreDuringCombat 경로는 이미 CancelRevive가 비운 뒤라 여기서는 아무 일도 없다.
        DisposeReviveCts();

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

        // 실제로 부서져 있던 타워만 예산을 소모한다(위 가드를 통과한 경우가 그렇다).
        if (cause == ReactivationCause.NightRevive)
        {
            _nightRevivesUsed++;
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
