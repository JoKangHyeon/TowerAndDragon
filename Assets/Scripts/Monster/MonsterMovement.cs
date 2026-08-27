using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 적 이동의 공통 기반(전략 패턴의 추상 클래스).
/// 구체 이동 방식(지상 스플라인 / 공중 직진)은 파생 클래스가 구현한다.
/// 목적지(메인 성 또는 경로 끝)에 도달하면 Arrived 이벤트를 발생시킨다.
/// </summary>
public abstract class MonsterMovement : MonoBehaviour
{
    protected const float DEFAULT_ARRIVAL_THRESHOLD = 0.05f;

    [SerializeField] protected float _arrivalThreshold = DEFAULT_ARRIVAL_THRESHOLD;

    protected float _speed;
    protected bool _isMoving;

    public bool HasArrived { get; protected set; }
    public UnityEvent Arrived;

    // 지금 실제로 전진 중인가(이동 애니메이션을 틀어야 하는 상태인가).
    // 두 조건이 모두 필요하다 - Stop()은 _speed를 그대로 두므로 속도만으로는 "공격하려고 멈춤"을
    // 알 수 없고, 빙결로 속도가 0이 되는 경우는 _isMoving이 true로 남으므로 _isMoving만으로도
    // 알 수 없다.
    public bool IsAdvancing => _isMoving && _speed > 0;

    // 지형 고저차만큼 들어올리기 전의 평면 좌표. 이 유닛이 "논리적으로 서 있는" 셀을 알아야 하는 쪽
    // (안개 틴트 등)이 화면 픽셀 기준으로 되짚지 않고 바로 쓰도록 노출한다 - 되짚으면 앞쪽 절벽에
    // 가려진 경우 엉뚱한 셀이 나온다. 들어올리지 않는 이동 방식은 현재 위치가 그대로 평면 좌표다.
    public virtual Vector3 GroundPlanePosition => transform.position;

    // 스프라이트 좌우 반전에 쓰는 진행 방향의 X 성분(부호만 의미 있음).
    // 정지 상태이거나 방향이 정해지지 않은 시점에는 0을 돌려주고, 보는 쪽이 마지막 방향을 유지한다.
    public virtual float MovementDirectionX => 0f;

    public virtual void SetSpeed(float speed)
    {
        _speed = speed;
    }

    public virtual float GetSpeed() => _speed;

    public virtual void Begin()
    {
        _isMoving = true;
    }

    public virtual void Stop()
    {
        _isMoving = false;
    }

    protected void RaiseArrived()
    {
        if (HasArrived)
        {
            return;
        }

        HasArrived = true;
        _isMoving = false;
        Arrived?.Invoke();
    }
}
