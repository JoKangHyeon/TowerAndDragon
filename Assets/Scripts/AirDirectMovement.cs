using UnityEngine;

/// <summary>
/// 공중 유닛 이동. 경로를 무시하고 메인 성을 향해 직진한다(까다로운 적).
/// 스포너가 목표(메인 성 Transform)를 주입한다.
/// </summary>
public class AirDirectMovement : MonsterMovement
{
    private Transform _target;

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    private void Update()
    {
        if (!_isMoving || _target == null)
        {
            return;
        }

        Vector3 destination = _target.position;
        transform.position = Vector3.MoveTowards(transform.position, destination, _speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, destination) <= _arrivalThreshold)
        {
            RaiseArrived();
        }
    }
}
