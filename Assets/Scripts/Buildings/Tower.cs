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

    public bool IsDead => _health == null || _health.IsDead;

    public TowerAttack Attack => _attack;

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

        _health.TakeDamage(damage.Amount);
    }

    private void HandleDisabled()
    {
        _attack.SetAttackEnabled(false);

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
        _health.RestoreToFull();
        _attack.SetAttackEnabled(_towerData.CanAttack);
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
