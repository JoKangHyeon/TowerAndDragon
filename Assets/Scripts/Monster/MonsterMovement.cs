using System;
using UnityEngine;

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
    public event Action Arrived;

    public virtual void SetSpeed(float speed)
    {
        _speed = speed;
    }

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
