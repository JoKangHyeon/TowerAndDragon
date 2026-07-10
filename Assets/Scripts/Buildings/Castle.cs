using System;
using UnityEngine;

/// <summary>메인 성 개체. HP는 Health에 위임하고, 0이 되면 Destroyed(= 게임 패배)를 발생시킨다.</summary>
[RequireComponent(typeof(Health))]
public class Castle : MonoBehaviour, IDamageable
{
    private const float DEFAULT_MAX_HEALTH = 100f;

    [SerializeField] private float _maxHealth = DEFAULT_MAX_HEALTH;

    private Health _health;

    public bool IsDead => _health == null || _health.IsDead;
    public float CurrentHealth => _health == null ? 0 : _health.CurrentHealth;
    public float MaxHealth => _health == null ? 0 : _health.MaxHealth;

    /// <summary>현재 체력, 최대 체력 순으로 전달.</summary>
    public event Action<float, float> HealthChanged;

    /// <summary>성 파괴 = 게임 패배.</summary>
    public event Action Destroyed;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _health.HealthChanged += HandleHealthChanged;
        _health.Died += HandleDestroyed;
    }

    private void Start()
    {
        // Awake가 아닌 Start에서 초기화 → 다른 컴포넌트가 초기 HealthChanged를 받도록.
        _health.Initialize(_maxHealth);
    }

    public void TakeDamage(DamageInfo damage)
    {
        // 죽음/음수 처리는 Health가 담당하므로 여기서 재검사하지 않는다.
        _health.TakeDamage(damage.Amount);
    }

    private void HandleHealthChanged(float current, float max)
    {
        HealthChanged?.Invoke(current, max);
    }

    private void HandleDestroyed()
    {
        // 게임오버 처리 연결 지점 (미구현).
        Destroyed?.Invoke();
    }

    private void OnDestroy()
    {
        if (_health != null)
        {
            _health.HealthChanged -= HandleHealthChanged;
            _health.Died -= HandleDestroyed;
        }
    }
}
