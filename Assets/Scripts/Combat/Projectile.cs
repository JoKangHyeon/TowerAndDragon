using UnityEngine;

public class Projectile : MonoBehaviour 
{
    private IAttackTarget _target;
    private AttackSO _attack;
    private AttackContext _context;
    private Vector3 _lastTargetPosition;
    private float _speed;
    private GameObject _targetObject;
    private Transform _targetTransform;
    private bool _isLaunched;

    public void Launch(
        IAttackTarget target, 
        AttackSO attack,
        in AttackContext context,
        float speed)
    {
        if (target == null || attack == null || speed <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        _targetObject = target.TargetObject;
        _targetTransform = target.TargetTransform;

        if (_targetObject == null || _targetTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        _target = target;
        _attack = attack;
        _context = context;
        _speed = speed;
        _lastTargetPosition = _targetTransform.position;
        _isLaunched = true;
    }

    private bool IsTargetAlive()
    {
        return _targetObject != null &&
            _targetTransform != null &&
            !_target.IsDead;
    }   

    private void Update()
    {
        if (!_isLaunched)
        {
            return;
        }

        if (IsTargetAlive())
        {
            _lastTargetPosition = _targetTransform.position;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            _lastTargetPosition,
            _speed * Time.deltaTime);

        if (transform.position != _lastTargetPosition)
        {
            return;
        }

        ApplyHit();
        Destroy(gameObject);
    }

    private void ApplyHit()
    {
        if (!IsTargetAlive())
        {
            return;
        }

        _attack.Execute(_target, in _context);
    }
}
