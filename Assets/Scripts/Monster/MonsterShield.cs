using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 선택 컴포넌트. 방어막을 두른 적에만 부착한다.
/// 데미지 파이프라인의 앞단에서 먼저 데미지를 흡수하고,
/// 방어막이 다 깎이면(파괴) 남은 데미지를 본체 체력으로 넘긴다.
/// 향후 "속성 방어막", "방어막 무시/증뎀 연구"는 여기서 처리한다.
/// </summary>
public class MonsterShield : MonoBehaviour, IShieldInfo
{
    private float _maxShield;
    private float _currentShield;

    public float MaxShield => _maxShield;
    public float CurrentShield => _currentShield;
    public bool IsBroken => _currentShield <= 0;

    // IShieldInfo - 체력바 UI가 숨김 여부를 판정하는 데 쓴다.
    // IsBroken(미초기화 시에도 true)과 달리, 방어막을 보유했는지/아직 안 깎였는지를 명확히 구분한다.
    public bool HasShield => _maxShield > 0;
    public bool IsIntact => _currentShield >= _maxShield;

    /// <summary>현재 방어막, 최대 방어막 순으로 전달.</summary>
    public UnityEvent<float, float> ShieldChanged;
    public UnityEvent ShieldBroken;

    public void Initialize(float amount)
    {
        _maxShield = amount;
        _currentShield = amount;
        ShieldChanged?.Invoke(_currentShield, _maxShield);
    }

    /// <summary>
    /// 데미지를 방어막으로 흡수하고, 흡수하지 못한 잔여 데미지를 반환한다.
    /// </summary>
    public float Absorb(float amount)
    {
        if (IsBroken)
        {
            return amount;
        }

        float absorbed = Mathf.Min(_currentShield, amount);
        _currentShield -= absorbed;
        ShieldChanged?.Invoke(_currentShield, _maxShield);

        if (IsBroken)
        {
            ShieldBroken?.Invoke();
        }

        return amount - absorbed;
    }
}
