using System;
using UnityEngine;

/// <summary>
/// 체력 컴포넌트. HP를 관리하고, HP가 0이 되면 Died 이벤트를 발생시킨다.
/// 사망 후처리(효과·삭제)는 이 이벤트를 구독하는 쪽(BaseMonster)이 담당한다.
/// </summary>
public class Health : MonoBehaviour
{
    private float _maxHealth;
    private float _currentHealth;

    public float MaxHealth => _maxHealth;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0;

    /// <summary>현재 체력, 최대 체력 순으로 전달.</summary>
    public event Action<float, float> HealthChanged;
    public event Action Died;

    public void Initialize(float maxHealth)
    {
        _maxHealth = maxHealth;
        _currentHealth = maxHealth;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) 
        {
            return;
        }

        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        HealthChanged?.Invoke(_currentHealth, _maxHealth);

        if (IsDead)
        {
            Died?.Invoke();
        }
    }

    public void RestoreToFull()
    {
        _currentHealth = _maxHealth;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0 || _currentHealth >= _maxHealth)
        {
            return;
        }

        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
    }
}
