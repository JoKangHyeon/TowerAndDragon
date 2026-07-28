using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 지상 유닛 이동. 포탈에서 메인 성까지 이어진 스플라인 경로를 따라 이동한다.
/// 스포너가 포탈별 경로(SplineContainer)를 주입한다.
/// 경로는 단차가 없는 평면 격자 위에 그려져 있으므로(노트 간격이 셀 종횡비 1:0.5와 정확히 일치),
/// 지나가는 셀의 고저차만큼 들어올려 언덕 위에서도 지형에 발을 붙이고 걷게 만든다.
/// </summary>
public class GroundSplineMovement : MonsterMovement
{
    // 고저차는 셀 단위로 계단처럼 끊겨 있어 샘플값을 그대로 쓰면 셀 경계마다 Y가 튄다 -
    // 목표 높이로 부드럽게 수렴시켜 경사면을 자연스럽게 오르내리게 한다. 값이 클수록 지형에 빠르게 붙는다.
    private const float DEFAULT_HEIGHT_FOLLOW_SPEED = 12f;

    [SerializeField] private float _heightFollowSpeed = DEFAULT_HEIGHT_FOLLOW_SPEED;

    private SplineContainer _path;
    private GridMap _gridMap;
    private float _splineLength;
    private float _distanceTraveled;
    private float _currentHeightOffset;
    private bool _hasHeightOffset;

    public override Vector3 GroundPlanePosition => transform.position - new Vector3(0f, _currentHeightOffset, 0f);

    private void Awake()
    {
        // GridMap이 없는 씬(팀원 테스트 씬 등)에서는 조용히 기존대로 평면 이동한다.
        _gridMap = FindFirstObjectByType<GridMap>();
    }

    public void SetPath(SplineContainer path)
    {
        _path = path;
        _splineLength = _path != null ? _path.CalculateLength() : 0;
        _distanceTraveled = 0;
        _hasHeightOffset = false;
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
        position.y += ResolveHeightOffset(position);
        transform.position = position;

        if (progress >= 1)
        {
            RaiseArrived();
        }
    }

    // 스플라인 위의 점은 아직 들어올리기 전의 평면 좌표이므로 평면 역변환(ConvertWorldToGrid)으로 셀을 찾는다.
    // 렌더된 화면 기준으로 타일을 고르는 PickCellAtWorldPoint를 쓰면 안 된다 - 그쪽은 이미 고저차가
    // 반영된 좌표를 입력으로 가정하므로, 여기서 쓰면 높이를 두 번 반영해 언덕에서 어긋난다.
    private float ResolveHeightOffset(Vector3 flatPosition)
    {
        if (_gridMap == null)
        {
            return 0f;
        }

        float targetHeightOffset = _gridMap.GetHeightOffset(_gridMap.ConvertWorldToGrid(flatPosition));

        // 스폰 직후 첫 프레임은 보간 없이 곧바로 지형 높이에 맞춘다 - 평지에서 솟아오르며 등장하지 않도록.
        if (!_hasHeightOffset)
        {
            _hasHeightOffset = true;
            _currentHeightOffset = targetHeightOffset;
            return _currentHeightOffset;
        }

        // 프레임률과 무관하게 같은 속도로 수렴하도록 지수 감쇠를 쓴다.
        _currentHeightOffset = Mathf.Lerp(
            _currentHeightOffset,
            targetHeightOffset,
            1f - Mathf.Exp(-_heightFollowSpeed * Time.deltaTime));

        return _currentHeightOffset;
    }
}
