using UnityEngine;

public class Projectile : MonoBehaviour 
{
    private IAttackTarget _target;
    private AttackSO _attack;
    private AttackContext _context;
    private Vector3 _lastTargetPosition;
    private float _speed;
    private float _remainingLifetime;
    private GameObject _targetObject;
    private Transform _targetTransform;
    private bool _isLaunched;

    public void Launch(
        IAttackTarget target, 
        AttackSO attack,
        in AttackContext context,
        float speed
    )
    {
        if (target == null)
        {
            return;
        }

        _targetObject = target.TargetObject;
        _targetTransform = target.TargetTransform;


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
        
        ApplyHit();
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