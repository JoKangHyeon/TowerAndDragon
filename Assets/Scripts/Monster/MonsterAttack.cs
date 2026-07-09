using UnityEngine;

/// <summary>
/// 선택 컴포넌트. 타워 공격형(원거리) 적에만 부착한다.
/// 지금은 공격 파라미터를 보관하는 골격만 둔다.
/// 대상 선정(가장 가까운 타워 등)과 발사 로직은 팀 확정 후 구현한다. [미정]
/// </summary>
public class MonsterAttack : MonoBehaviour
{
    private float _damage;
    private float _range;
    private float _interval;

    public float Damage => _damage;
    public float Range => _range;
    public float Interval => _interval;

    public void Initialize(float damage, float range, float interval)
    {
        _damage = damage;
        _range = range;
        _interval = interval;
    }
}
