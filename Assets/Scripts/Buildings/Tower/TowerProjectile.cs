using UnityEngine;

/// <summary>
/// 타워 공격의 시각적 투사체. 피해는 발사 시점에 TowerAttack이 즉시 적용하며,
/// 이 컴포넌트는 대상까지 이동하는 표현만 담당한다.
/// </summary>
public class TowerProjectile : MonoBehaviour
{
    private Transform _target;
    private Vector3 _targetPosition;
    private float _speed;
    private bool _isLaunched;

    public void Launch(Transform target, float speed)
    {
        _target = target;
        _speed = speed;

        if (_target == null || _speed <= 0)
        {
            Destroy(gameObject);
            return;
        }

        _targetPosition = _target.position;
        _isLaunched = true;
    }

    private void Update()
    {
        if (!_isLaunched)
        {
            return;
        }

        if (_target != null)
        {
            _targetPosition = _target.position;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            _targetPosition,
            _speed * Time.deltaTime);

        if (transform.position == _targetPosition)
        {
            Destroy(gameObject);
        }
    }
}
