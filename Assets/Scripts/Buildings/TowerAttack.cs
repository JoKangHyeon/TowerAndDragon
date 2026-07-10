using UnityEngine;

public class TowerAttack : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers;

    private AttackSO _attack;
    private BaseMonster _target;
    private float _nextAttackTime;
    private bool _isAttackEnabled;

    [SerializeField] private bool _showDebugLogs;

    public void Initialize(AttackSO attack)
    {
        _attack = attack;
        _target = null;
        _nextAttackTime = Time.time;
        _isAttackEnabled = _attack != null;
    }

    public void SetAttackEnabled(bool isEnabled)
    {
        _isAttackEnabled = isEnabled && _attack != null;

        if (!_isAttackEnabled)
        {
            _target = null;
        }
    }

    private void Update()
    {
        if (!_isAttackEnabled)
        {
            return;
        }

        if (!IsCurrentTargetValid())
        {
            _target = FindClosestTarget();

            // 타워 공격 디버깅용
            if (_showDebugLogs && _target != null)
            {
                Debug.Log($"[TowerAttack] {name}이(가) 타겟 {_target.name} 선정했습니다.", this);
            }
            //까지
        }

        if (_target == null || Time.time < _nextAttackTime)
        {
            return;
        }

        Fire();
        _nextAttackTime = Time.time + _attack.Interval;
    }

    private bool IsCurrentTargetValid()
    {
        if (_target == null || _target.IsDead)
        {
            return false;
        }

        return GetSqrDistance(_target.transform.position) <= _attack.Range * _attack.Range;
    }

    // 후에 몬스터의 종류, 및 타워종류에 따라 공격 우선도 다르게
    private BaseMonster FindClosestTarget()
    {
        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            transform.position,
            _attack.Range,
            _targetLayers);

        BaseMonster closestTarget = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (Collider2D candidate in candidates)
        {
            BaseMonster monster = candidate.GetComponentInParent<BaseMonster>();
            if (monster == null || monster.IsDead)
            {
                continue;
            }

            float sqrDistance = GetSqrDistance(monster.transform.position);
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestTarget = monster;
            closestSqrDistance = sqrDistance;
        }

        return closestTarget;
    }

    private float GetSqrDistance(Vector3 targetPosition)
    {
        return (targetPosition - transform.position).sqrMagnitude;
    }

    private void Fire()
    {
        //타워 디버깅용
        if (_showDebugLogs)
        {
            Debug.Log(
                $"[TowerAttack] {name} → {_target.name} 공격 " +
                $"(거리: {Vector3.Distance(transform.position, _target.transform.position):F2}, " +
                $"다음 공격 간격: {_attack.Interval:F2}초)", this);
        }
        //까지
        AttackContext context = new AttackContext(gameObject);
        _attack.Execute(_target, in context);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_attack == null)
        {
            return;
        }
    
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attack.Range);
    }
#endif
}
