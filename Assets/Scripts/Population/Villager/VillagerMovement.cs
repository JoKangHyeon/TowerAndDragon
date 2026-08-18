using DG.Tweening;
using UnityEngine;

/// <summary>
/// 인구 배치 연출용 지상 직선 이동.
///
/// <see cref="MonsterMovement"/>를 상속하는 이유는 몬스터라서가 아니라, 이 기반 클래스가
/// "아이소메트릭 지상 이동" 계약(GroundPlanePosition / MovementDirectionX / Arrived) 그 자체이기
/// 때문이다. 상속하면 <see cref="MonsterSpriteFlipper"/>(좌우 반전)와
/// <see cref="IsometricDepthSorter"/>(깊이 정렬)가 코드 없이 그대로 붙는다.
/// 전투 관련 요소(Health, IAttackTarget, MonsterAttack)에는 전혀 의존하지 않는다.
///
/// 목적지를 월드 좌표가 아니라 "셀"로 받는다 - GridMap.ConvertGridToWorld / GetChunkCenterWorld /
/// GetFootprintCenterWorld는 모두 고저차가 이미 더해진 값을 돌려주므로, 그 값을 목적지로 쓰면서
/// 여기서 다시 고저차를 더하면 언덕에서 캐릭터가 두 배로 떠오른다. 셀만 받으면 평탄화 계산이
/// 이 클래스 하나에만 존재해 이중 반영이 구조적으로 불가능하다.
/// </summary>
public sealed class VillagerMovement : MonsterMovement
{
    // 고저차는 셀 단위로 계단처럼 끊겨 있어 샘플값을 그대로 쓰면 셀 경계마다 Y가 튄다 -
    // 목표 높이로 부드럽게 수렴시킨다. GroundSplineMovement와 같은 값을 쓴다.
    private const float DEFAULT_HEIGHT_FOLLOW_SPEED = 12f;
    private const float DEFAULT_MAX_TRAVEL_SECONDS = 2f;
    private const float DEFAULT_MAX_DISTANCE_SPEED_MULTIPLIER = 5f;
    private const float DEFAULT_BOUNCE_STEP_SECONDS = 0.2f;
    private const float DEFAULT_BOUNCE_SQUASH_X = 1.08f;
    private const float DEFAULT_BOUNCE_SQUASH_Y = 0.92f;
    private const float DEFAULT_BOUNCE_STRETCH_X = 0.96f;
    private const float DEFAULT_BOUNCE_STRETCH_Y = 1.08f;
    private const float MIN_DISTANCE_SPEED_MULTIPLIER = 1f;
    private const int INFINITE_LOOP_COUNT = -1;

    [SerializeField] private float _heightFollowSpeed = DEFAULT_HEIGHT_FOLLOW_SPEED;
    [SerializeField] private float _maxTravelSeconds = DEFAULT_MAX_TRAVEL_SECONDS;
    [SerializeField] private float _maxDistanceSpeedMultiplier = DEFAULT_MAX_DISTANCE_SPEED_MULTIPLIER;
    [SerializeField] private float _bounceStepSeconds = DEFAULT_BOUNCE_STEP_SECONDS;
    [SerializeField] private Vector2 _bounceSquashScale =
        new Vector2(DEFAULT_BOUNCE_SQUASH_X, DEFAULT_BOUNCE_SQUASH_Y);
    [SerializeField] private Vector2 _bounceStretchScale =
        new Vector2(DEFAULT_BOUNCE_STRETCH_X, DEFAULT_BOUNCE_STRETCH_Y);

    private GridMap _gridMap;

    // 고저차를 더하기 전의 논리 좌표. 이동·도착 판정은 전부 이 좌표로 한다.
    private Vector3 _flatPosition;
    private Vector3 _flatDestination;

    private float _baseSpeed;
    private float _currentHeightOffset;
    private Vector3 _baseLocalScale;
    private Tween _bounceTween;
    private bool _hasHeightOffset;
    private bool _hasDestination;
    private bool _hasBaseLocalScale;

    public override Vector3 GroundPlanePosition => _flatPosition;

    public override float MovementDirectionX =>
        _isMoving && _hasDestination ? _flatDestination.x - _flatPosition.x : 0f;

    public override void SetSpeed(float speed)
    {
        _baseSpeed = speed;
        _speed = speed;
    }

    public override void Begin()
    {
        base.Begin();
        StartBounceTween();
    }

    public override void Stop()
    {
        base.Stop();
        StopBounceTween();
    }

    /// <summary>스포너(VillagerDispatchSystem)가 주입한다. GroundSplineMovement처럼 스스로 찾지 않는
    /// 이유: 이 컴포넌트는 항상 GridMap을 이미 알고 있는 시스템이 생성하므로 씬 전역 검색이 낭비다.</summary>
    public void Construct(GridMap gridMap)
    {
        _gridMap = gridMap;
    }

    /// <summary>보간 없이 즉시 그 자리에 놓는다. 스폰 지점 지정과, 세이브 복원 시의
    /// "걸어오지 않고 제자리에서 일하는 상태로 등장"에 쓴다.</summary>
    public void Warp(Vector3Int cell, Vector3 spreadOffset)
    {
        _flatPosition = ToFlatWorld(cell) + spreadOffset;
        _hasHeightOffset = false;
        ApplyPosition();
    }

    public void WarpWorld(Vector3 worldPosition)
    {
        if (_gridMap == null)
        {
            _flatPosition = worldPosition;
            _currentHeightOffset = 0f;
        }
        else
        {
            Vector3Int cell = _gridMap.PickCellAtWorldPoint(worldPosition);
            _currentHeightOffset = _gridMap.GetHeightOffset(cell);
            _flatPosition = worldPosition - new Vector3(0f, _currentHeightOffset, 0f);
        }

        _hasHeightOffset = true;
        transform.position = worldPosition;
    }

    /// <summary>다음 구간의 목적지를 정한다. <see cref="MonsterMovement.HasArrived"/>가 되돌아가므로
    /// 성 → 건물 → 성처럼 여러 구간을 한 인스턴스로 이어 달릴 수 있다(재스폰 불필요).</summary>
    public void SetDestination(Vector3Int cell, Vector3 spreadOffset)
    {
        SetFlatDestination(ToFlatWorld(cell) + spreadOffset);
    }

    public void SetDestinationWorld(Vector3 worldPosition)
    {
        if (_gridMap == null)
        {
            _flatDestination = worldPosition;
        }
        else
        {
            Vector3Int cell = _gridMap.PickCellAtWorldPoint(worldPosition);
            float heightOffset = _gridMap.GetHeightOffset(cell);
            _flatDestination = worldPosition - new Vector3(0f, heightOffset, 0f);
        }

        SetFlatDestination(_flatDestination);
    }

    private void SetFlatDestination(Vector3 flatDestination)
    {
        _flatDestination = flatDestination;
        _hasDestination = true;
        HasArrived = false;
        RefreshSegmentSpeed();
    }

    private void RefreshSegmentSpeed()
    {
        if (_baseSpeed <= 0f)
        {
            _speed = _baseSpeed;
            return;
        }

        float distance = Vector3.Distance(_flatPosition, _flatDestination);
        float speedForTravelTime = _maxTravelSeconds > 0f ? distance / _maxTravelSeconds : _baseSpeed;
        float maxSpeed = _baseSpeed * Mathf.Max(MIN_DISTANCE_SPEED_MULTIPLIER, _maxDistanceSpeedMultiplier);

        _speed = Mathf.Clamp(
            Mathf.Max(_baseSpeed, speedForTravelTime),
            _baseSpeed,
            maxSpeed);
    }

    private void Update()
    {
        if (!_isMoving || !_hasDestination)
        {
            return;
        }

        _flatPosition = Vector3.MoveTowards(_flatPosition, _flatDestination, _speed * Time.deltaTime);
        ApplyPosition();

        // 도착 판정은 반드시 평면 좌표로 한다 - transform.position에는 고저차가 섞여 있어
        // 출발지와 목적지의 높이가 다르면 목적지에 닿아도 임계값 안에 영영 들어오지 못한다.
        if (Vector3.Distance(_flatPosition, _flatDestination) <= _arrivalThreshold)
        {
            StopBounceTween();
            RaiseArrived();
        }
    }

    private void OnDisable()
    {
        StopBounceTween();
    }

    private void OnDestroy()
    {
        StopBounceTween();
    }

    private void StartBounceTween()
    {
        if (_bounceStepSeconds <= 0f)
        {
            return;
        }

        EnsureBaseLocalScale();

        if (_bounceTween != null && _bounceTween.IsActive())
        {
            return;
        }

        _bounceTween?.Kill();
        _bounceTween = DOTween.Sequence()
            .Append(transform.DOScale(ResolveBounceScale(_bounceSquashScale), _bounceStepSeconds)
                .SetEase(Ease.OutQuad))
            .Append(transform.DOScale(ResolveBounceScale(_bounceStretchScale), _bounceStepSeconds)
                .SetEase(Ease.OutBack))
            .Append(transform.DOScale(_baseLocalScale, _bounceStepSeconds)
                .SetEase(Ease.InOutQuad))
            .SetLoops(INFINITE_LOOP_COUNT, LoopType.Restart);
    }

    private void StopBounceTween()
    {
        _bounceTween?.Kill();
        _bounceTween = null;

        if (_hasBaseLocalScale)
        {
            transform.localScale = _baseLocalScale;
        }
    }

    private void EnsureBaseLocalScale()
    {
        if (_hasBaseLocalScale)
        {
            return;
        }

        _baseLocalScale = transform.localScale;
        _hasBaseLocalScale = true;
    }

    private Vector3 ResolveBounceScale(Vector2 scaleMultiplier)
    {
        return new Vector3(
            _baseLocalScale.x * scaleMultiplier.x,
            _baseLocalScale.y * scaleMultiplier.y,
            _baseLocalScale.z);
    }

    private void ApplyPosition()
    {
        transform.position = _flatPosition + new Vector3(0f, ResolveHeightOffset(_flatPosition), 0f);
    }

    // 셀 중심의 "평면" 월드 좌표. ConvertGridToWorld가 더해준 고저차를 그대로 빼서 되돌린다 -
    // 평면 역변환(ConvertWorldToGrid)으로 되짚는 것보다 정확하다(정변환의 역산이라 오차가 없다).
    private Vector3 ToFlatWorld(Vector3Int cell)
    {
        if (_gridMap == null)
        {
            return Vector3.zero;
        }

        return _gridMap.ConvertGridToWorld(cell) - new Vector3(0f, _gridMap.GetHeightOffset(cell), 0f);
    }

    // GroundSplineMovement.ResolveHeightOffset과 같은 규칙이다. 평면 좌표이므로 평면 역변환으로 셀을 찾는다.
    // PickCellAtWorldPoint를 쓰면 안 된다 - 그쪽은 이미 고저차가 반영된 좌표를 입력으로 가정하므로
    // 여기서 쓰면 높이를 두 번 반영해 언덕에서 어긋난다.
    private float ResolveHeightOffset(Vector3 flatPosition)
    {
        if (_gridMap == null)
        {
            return 0f;
        }

        float targetHeightOffset = _gridMap.GetHeightOffset(_gridMap.ConvertWorldToGrid(flatPosition));

        // 등장 첫 프레임은 보간 없이 곧바로 지형 높이에 맞춘다 - 평지에서 솟아오르며 나타나지 않도록.
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
