using UnityEngine;

/// <summary>
/// 몬스터 공격의 투사체. 타워 투사체(시각 표현 전용)와 달리
/// 공격(AttackSO)과 컨텍스트를 들고 날아가 명중 시점에 피해를 적용한다.
/// 대상이 비행 중 파괴·사망하면 마지막 위치까지 이동한 뒤 피해 없이 소멸한다.
/// </summary>
public class MonsterProjectile : MonoBehaviour
{
    private IMonsterTarget _target;
    private AttackSO _attack;
    private AttackContext _context;
    private Vector3 _targetPosition;
    private float _speed;
    private bool _isLaunched;

    public void Launch(IMonsterTarget target, AttackSO attack, in AttackContext context, float speed)
    {
        _target = target;
        _attack = attack;
        _context = context;
        _speed = speed;

        if (_target == null ||
            _target.TargetTransform == null ||
            _attack == null ||
            _speed <= 0)
        {
            Destroy(gameObject);
            return;
        }

        _targetPosition = _target.TargetTransform.position;
        _isLaunched = true;
    }

    private void Update()
    {
        if (!_isLaunched)
        {
            return;
        }

        if (IsTargetAlive())
        {
            _targetPosition = _target.TargetTransform.position;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            _targetPosition,
            _speed * Time.deltaTime);

        if (transform.position == _targetPosition)
        {
            ApplyHit();
            Destroy(gameObject);
        }
    }

    private bool IsTargetAlive()
    {
        if (_target is Object targetObject && targetObject == null)
        {
            return false;
        }

        return !_target.IsDead && _target.TargetTransform != null;
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
