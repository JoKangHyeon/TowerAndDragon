using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(TowerAttack))]
public class Tower : Building, IDamageable
{

    [SerializeField] private TowerData _towerData;

    private Health _health;
    private TowerAttack _attack;
    private Coroutine _reviveCoroutine;
    private bool _isInitialized;

    public bool IsDead => _health == null || _health.IsDead;

    public TowerAttack Attack => _attack;

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
        _attack.Initialize(_towerData.Attack);
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

        if (_reviveCoroutine != null)
        {
            StopCoroutine(_reviveCoroutine);
        }

        _reviveCoroutine = StartCoroutine(ReviveAfterDelay());
    }

    public void RestoreAtMorning()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (_reviveCoroutine != null)
        {
            StopCoroutine(_reviveCoroutine);
            _reviveCoroutine = null;
        }

        RestoreAndReactivate();
    }

    private IEnumerator ReviveAfterDelay()
    {
        yield return new WaitForSeconds(_towerData.ReviveDelay);
        _reviveCoroutine = null;
        RestoreAndReactivate();
    }

    private void RestoreAndReactivate()
    {
        _health.RestoreToFull();
        _attack.SetAttackEnabled(_towerData.CanAttack);
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.Died -= HandleDisabled;
        }
    }
}
