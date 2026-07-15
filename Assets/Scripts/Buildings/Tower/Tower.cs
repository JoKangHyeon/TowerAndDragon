using System;
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
    private bool _isInitialized;
    private bool _isDisabled;
    private float _disabledAtTime;

    public bool IsDead => _health == null || _health.IsDead;

    public TowerAttack Attack => _attack;
    public TowerData Data => _towerData;

    public MonsterTargetType TargetType => MonsterTargetType.Tower;
    public Transform TargetTransform => transform;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _attack = GetComponent<TowerAttack>();
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
            _health.Died -= HandleDisabled;
        }

        _towerData = data;
        _health.Initialize(_towerData.MaxHealth);
        _health.Died += HandleDisabled;
        _attack.Initialize(_towerData);
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

        Debug.Log(
            $"[Tower] {name}이 재활성화되었습니다. 실제 비활성화 시간: {disabledDuration:F2}초",
            this);
    }

    private void OnDestroy()
    {
        CancelRevive();

        if (_health != null)
        {
            _health.Died -= HandleDisabled;
        }
    }
}
