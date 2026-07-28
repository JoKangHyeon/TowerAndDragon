using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(TowerAttack))]
public class Tower : Building, IMonsterTarget
{
    [SerializeField] private TowerData _towerData;
    private Health _health;
    private TowerAttack _attack;
    private CancellationTokenSource _reviveCts;
    private Animator _animator;

    private bool _isInitialized;
    private bool _isDisabled;
    private float _disabledAtTime;

    public bool IsDead => _health == null || _health.IsDead;
    public TowerAttack Attack => _attack;
    public TowerData Data => _towerData;
    public override IReadOnlyList<ResourceAmount> BuildCost => _towerData != null ? _towerData.BuildCost : base.BuildCost;
    public override int PopulationCapacity => _towerData != null ? _towerData.PopulationCapacity : base.PopulationCapacity;
    public MonsterTargetType TargetType => MonsterTargetType.Tower;

    // 인구로 가동하지 않는 타워(새끼용 등)는 false로 override한다.
    public virtual bool RequiresPopulation => true;
    public Transform TargetTransform => transform;
    public GameObject TargetObject => gameObject;

    private static readonly int HIT_ANIM_KEY = Animator.StringToHash("Hit");
    private static readonly int BROKEN_ANIM_KEY = Animator.StringToHash("Broken");


    private void Awake()
    {
        _health = GetComponent<Health>();
        _attack = GetComponent<TowerAttack>();
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

        _towerData = data;
        _health.Initialize(_towerData.MaxHealth);
        _health.Died.AddListener(HandleDisabled);
        _attack.Initialize(_towerData, _animator);
        _isInitialized = true;
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

    private void HandleDisabled()
    {
        _attack.SetAttackEnabled(false);
        _isDisabled = true;
        _disabledAtTime = Time.time;

        Debug.Log(
            $"[Tower] {name}이 비활성화되었습니다. 재활성화 대기시간: {_towerData.ReviveDelay}초",
            this);

        CancelRevive();
        _reviveCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        ReviveAfterDelayAsync(_reviveCts.Token).Forget();

        if (_animator != null)
        {
            _animator.SetBool(BROKEN_ANIM_KEY, true);
        }
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
        await UniTask.Delay(TimeSpan.FromSeconds(_towerData.ReviveDelay), cancellationToken: token);
        RestoreAndReactivate();
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

        _health.RestoreToFull();
        _attack.SetAttackEnabled(_towerData.CanAttack);
        _isDisabled = false;

        if (!wasDisabled)
        {
            return;
        }

        if (_animator != null)
        {
            _animator.SetBool(BROKEN_ANIM_KEY, false);
        }

        Debug.Log(
            $"[Tower] {name}이 재활성화되었습니다. 실제 비활성화 시간: {disabledDuration:F2}초",
            this);
    }

    private void OnDestroy()
    {
        CancelRevive();

        if (_health != null)
        {
            _health.Died.RemoveListener(HandleDisabled);
        }
    }
}
