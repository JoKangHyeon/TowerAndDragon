using System;
using UnityEngine;

/// <summary>
/// 타겟을 쫓아가 명중 시점에 피해를 적용하는 투사체. 타워·새끼용(TowerAttack)과 몬스터(MonsterAttack)가
/// 공유한다.
///
/// 스스로를 파괴하지 않고 <see cref="BindRelease"/>로 주입받은 창구로 돌아간다 - 어디서 나왔는지를
/// 만든 쪽만 알기 때문이다(<see cref="ProjectilePool"/>). 그래서 자기 풀이 아닌 곳으로 반납되는
/// 사고가 구조적으로 생기지 않는다.
/// </summary>
public class Projectile : MonoBehaviour
{
    // Vector3.normalized는 크기가 1e-5 미만이면 정규화 대신 zero를 준다. 그 구간을 걸러내지 않으면
    // 진행 방향이 0이 되어 Atan2(0, 0)이 각도를 0도로 만든다. 제곱으로 비교하므로 임계값도 제곱한 값이다.
    private const float MIN_AIM_DELTA_SQR = 1e-10f;

    private IAttackTarget _target;
    private AttackSO _attack;
    private AttackContext _context;
    private Vector3 _lastTargetPosition;
    private float _speed;
    private GameObject _targetObject;
    private Transform _targetTransform;
    private bool _isLaunched;

    // 만든 쪽이 주입하는 반납 창구. 널이면 스스로 파괴한다 - 풀을 거치지 않고 Instantiate된 경우다.
    private Action<Projectile> _release;

    // 화면상 진행 방향. 명중 이펙트를 같은 각도로 놓기 위해 명중 시점까지 들고 있는다.
    private Vector3 _travelDirection;

    private ProjectileVisual _visual;

    // 명중 시점에 낼 소리. 쏜 쪽의 데이터라 투사체 프리팹이 아니라 발사할 때 받아 둔다.
    private SoundId? _resolveSound;

    /// <summary>만든 쪽이 반납 창구를 알려 준다. 재사용할 때마다 다시 불려도 무해하다.</summary>
    public void BindRelease(Action<Projectile> release)
    {
        _release = release;
    }

    public void Launch(
        IAttackTarget target,
        AttackSO attack,
        in AttackContext context,
        float speed,
        SoundId? resolveSound = null)
    {
        // 재사용된 인스턴스는 지난번 발사의 상태를 그대로 들고 있다 - 무엇보다 _isLaunched가
        // 켜진 채로 남아 있어, 아래 실패 경로로 빠져도 Update가 옛 타겟을 향해 돌기 시작한다.
        ResetState();

        if (target == null || attack == null || speed <= 0f)
        {
            Release();
            return;
        }

        _targetObject = target.TargetObject;
        _targetTransform = target.TargetTransform;

        if (_targetObject == null || _targetTransform == null)
        {
            Release();
            return;
        }

        _target = target;
        _attack = attack;
        _context = context;
        _speed = speed;
        _resolveSound = resolveSound;
        _lastTargetPosition = _targetTransform.position;
        _isLaunched = true;

        AimAtTravelDirection();

        // 방향을 정한 뒤에 알린다 - 총구 섬광이 발사 방향으로 돌아가야 한다.
        _visual?.OnLaunched(_travelDirection);
    }

    // 연출은 프리팹마다 붙어 있을 수도, 없을 수도 있다(연출을 넣지 않은 옛 투사체).
    // 없는 경우 지연 조회로 두면 매번 GetComponent를 다시 타므로 여기서 한 번에 정한다.
    private void Awake()
    {
        _visual = GetComponent<ProjectileVisual>();
    }

    private void ResetState()
    {
        _isLaunched = false;
        _target = null;
        _attack = null;
        _targetObject = null;
        _targetTransform = null;
        _lastTargetPosition = Vector3.zero;
        _speed = 0f;
        _travelDirection = Vector3.zero;
        _resolveSound = null;
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

        // 타겟이 움직이면 겨눌 방향도 바뀐다 - 이동보다 먼저 돌려야 이번 프레임의 이동과 방향이 맞는다.
        AimAtTravelDirection();

        transform.position = Vector3.MoveTowards(
            transform.position,
            _lastTargetPosition,
            _speed * Time.deltaTime);

        if (transform.position != _lastTargetPosition)
        {
            return;
        }

        ApplyHit();

        _visual?.OnHit(transform.position, _travelDirection, _targetObject);

        // 명중 VFX와 같은 프레임에 낸다. 날아가는 동안 타겟이 죽어도 VFX가 목적지에서 터지는 규칙에 맞춘다.
        if (_resolveSound is SoundId resolveSound)
        {
            SoundManager.Play(resolveSound, transform.position);
        }

        Release();
    }

    // 화면에서 보이는 진행 방향으로 돌린다. 아이소메트릭 타일이 세로로 눌려 있어 사거리 판정은
    // 타원을 쓰지만(TowerAttack.IsWithinAttackRange) 여기서는 그 보정을 하지 않는다 -
    // MoveTowards가 월드 좌표에서 직선으로 가므로 화면에서도 직선이고, 그 각도가 곧 보이는 각도다.
    private void AimAtTravelDirection()
    {
        Vector3 delta = _lastTargetPosition - transform.position;

        // 도착 직전에는 delta가 0에 수렴한다 - 그 값으로 각도를 다시 구하면 방향이 튄다.
        if (delta.sqrMagnitude <= MIN_AIM_DELTA_SQR)
        {
            return;
        }

        _travelDirection = delta.normalized;

        float degrees = Mathf.Atan2(_travelDirection.y, _travelDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, degrees);
    }

    private void ApplyHit()
    {
        if (!IsTargetAlive())
        {
            return;
        }

        _attack.Execute(_target, in _context);
    }

    private void Release()
    {
        _isLaunched = false;

        if (_release != null)
        {
            _release(this);
            return;
        }

        Destroy(gameObject);
    }
}
