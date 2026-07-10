using UnityEngine;

public class TowerAttack : MonoBehaviour
{
    [SerializeField] private LayerMask _targetLayers;

    private AttackSO _attack;
    private BaseMonster _target;
    private float _nextAttackTime;
    private bool _isAttackEnabled;

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
        AttackContext context = new AttackContext(gameObject);
        _attack.Execute(_target, in context);
    }
}
