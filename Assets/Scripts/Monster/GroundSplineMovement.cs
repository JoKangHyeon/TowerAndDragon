using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 지상 유닛 이동. 포탈에서 메인 성까지 이어진 스플라인 경로를 따라 이동한다.
/// 스포너가 포탈별 경로(SplineContainer)를 주입한다.
/// </summary>
public class GroundSplineMovement : MonsterMovement
{
    private SplineContainer _path;
    private float _splineLength;
    private float _distanceTraveled;

    public void SetPath(SplineContainer path)
    {
        _path = path;
        _splineLength = _path != null ? _path.CalculateLength() : 0;
        _distanceTraveled = 0;
    }

    private void Update()
    {
        if (!_isMoving || _path == null || _splineLength <= 0)
        {
            return;
        }

        _distanceTraveled += _speed * Time.deltaTime;
        float progress = Mathf.Clamp01(_distanceTraveled / _splineLength);

        Vector3 position = _path.EvaluatePosition(progress);
        transform.position = position;

        if (progress >= 1)
        {
            RaiseArrived();
        }
    }
}
