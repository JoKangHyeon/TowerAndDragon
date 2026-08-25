using UnityEngine;

/// <summary>
/// 투사체의 연출 담당. <see cref="Projectile"/>이 발사·명중 시점에 알려 주고, 무엇을 보여줄지는
/// 전부 이쪽이 정한다. 없어도 투사체는 그대로 날아가므로 연출을 넣지 않은 프리팹에는 붙이지 않는다.
///
/// 궤적은 이 오브젝트의 자식으로 두고 <b>명중 시 떼어내지 않는다</b>. 에셋에 딸려온 무버
/// (Hovl HS_ProjectileMover)는 자식을 unparent해서 남은 입자를 마저 재생시키지만, 그러면 풀이
/// 자식 하나가 사라진 오브젝트를 돌려받는다. "명중 후에도 남는 연출"은 궤적 잔상이 아니라
/// 별도 명중 이펙트가 담당한다.
/// </summary>
[RequireComponent(typeof(Projectile))]
public sealed class ProjectileVisual : MonoBehaviour
{
    private const float DEFAULT_MUZZLE_LIFETIME_SECONDS = 0.5f;
    private const float DEFAULT_IMPACT_LIFETIME_SECONDS = 1f;

    [Tooltip("날아가는 동안 재생할 궤적 파티클. **비워 두면 자식 파티클 전부를 쓴다** - " +
             "일부만 골라 쓰고 싶을 때만 채운다.")]
    [WiringOptional]
    [SerializeField] private ParticleSystem[] _trailParticles;

    [Tooltip("발사 순간 총구에 띄울 이펙트. 비워 두면 생략된다. " +
             "자식으로 두지 말 것 - 투사체를 따라다니면 총구 섬광이 아니게 된다.")]
    [WiringOptional]
    [SerializeField] private GameObject _muzzlePrefab;

    [Tooltip("발사 이펙트가 스스로 걷히기까지의 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _muzzleLifetimeSeconds = DEFAULT_MUZZLE_LIFETIME_SECONDS;

    [Tooltip("명중 지점에 띄울 이펙트. 비워 두면 명중 연출만 생략된다.")]
    [WiringOptional]
    [SerializeField] private GameObject _impactPrefab;

    [Tooltip("명중 이펙트가 스스로 걷히기까지의 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _impactLifetimeSeconds = DEFAULT_IMPACT_LIFETIME_SECONDS;

    [Tooltip("켜면 명중 이펙트를 투사체가 날아온 방향으로 돌려 놓는다. " +
             "사방으로 터지는 폭발형은 꺼도 차이가 없다.")]
    [SerializeField] private bool _rotateImpactToTravelDirection = true;

    [Header("Impact Placement")]
    [Tooltip("명중 연출의 시각 위치. 피해 판정 위치에는 영향을 주지 않는다.")]
    [SerializeField] private ProjectileImpactPlacement _impactPlacement;

    /// <summary>발사 직후. 풀에서 꺼낸 인스턴스는 지난번 입자를 들고 있으므로 되감아 재생한다.</summary>
    public void OnLaunched(Vector3 travelDirection)
    {
        PlayTrails();
        PlayMuzzle(travelDirection);
    }

    // 비워 둔 경우 자식 파티클을 전부 궤적으로 본다 - 프리팹마다 배열을 손으로 채우지 않아도
    // 되도록 한 것이다. 한 번만 찾아 캐시한다(자식 구성은 런타임에 바뀌지 않는다).
    private void Awake()
    {
        if (_trailParticles == null || _trailParticles.Length == 0)
        {
            _trailParticles = GetComponentsInChildren<ParticleSystem>(true);
        }
    }

    private void PlayTrails()
    {
        if (_trailParticles == null)
        {
            return;
        }

        foreach (ParticleSystem trail in _trailParticles)
        {
            if (trail == null)
            {
                continue;
            }

            // Clear를 빼면 월드 공간 파티클이 직전 명중 지점에서 이번 발사 지점까지 줄을 긋는다.
            trail.Clear(true);
            trail.Play(true);
        }
    }

    // 총구 섬광은 발사 자리에 남고 투사체는 떠난다 - 그래서 자식이 아니라 별도 인스턴스다.
    //
    // 여기서 쓰는 transform.position이 곧 타워의 FirePoint다. 투사체를 그 자리에 놓아 주는 것이
    // 스폰하는 쪽(TowerAttack·MonsterAttack이 _firePoint.position을 넘긴다)이고, 이 함수는
    // 아직 한 프레임도 움직이지 않은 시점에 불리기 때문이다.
    private void PlayMuzzle(Vector3 travelDirection)
    {
        if (_muzzlePrefab == null)
        {
            return;
        }

        ProjectilePool.PlayForSeconds(
            _muzzlePrefab,
            transform.position,
            ResolveDirectionRotation(travelDirection),
            _muzzleLifetimeSeconds);
    }

    /// <summary>
    /// 명중 직후, 투사체가 반납되기 직전. 궤적을 멈추고 명중 이펙트를 띄운다.
    /// </summary>
    public void OnHit(
        Vector3 hitPosition,
        Vector3 travelDirection,
        GameObject targetObject)
    {
        StopTrails();

        Vector3 visualImpactPosition = ResolveVisualImpactPosition(hitPosition, targetObject);

        if (_impactPrefab == null)
        {
            return;
        }

        ProjectilePool.PlayForSeconds(
            _impactPrefab,
            visualImpactPosition,
            _rotateImpactToTravelDirection
                ? ResolveDirectionRotation(travelDirection)
                : Quaternion.identity,
            _impactLifetimeSeconds);
    }

    private Vector3 ResolveVisualImpactPosition(Vector3 hitPosition, GameObject targetObject)
    {
        if (_impactPlacement == ProjectileImpactPlacement.TargetOrigin || targetObject == null)
        {
            return hitPosition;
        }

        SpriteRenderer bodyRenderer = targetObject.GetComponent<SpriteRenderer>();

        if (bodyRenderer == null)
        {
            bodyRenderer = targetObject.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (bodyRenderer == null)
        {
            return hitPosition;
        }

        Bounds bounds = bodyRenderer.bounds;

        return _impactPlacement == ProjectileImpactPlacement.Ground
            ? new Vector3(bounds.center.x, bounds.min.y, hitPosition.z)
            : new Vector3(bounds.center.x, bounds.center.y, hitPosition.z);
    }

    private void StopTrails()
    {
        if (_trailParticles == null)
        {
            return;
        }

        foreach (ParticleSystem trail in _trailParticles)
        {
            if (trail == null)
            {
                continue;
            }

            trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    // 진행 방향을 Z축 회전으로 바꾼다. Atan2 기준 0°가 +X이므로 프리팹도 +X를 앞으로 보고 만든다.
    private static Quaternion ResolveDirectionRotation(Vector3 travelDirection)
    {
        if (travelDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return Quaternion.identity;
        }

        float degrees = Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg;

        return Quaternion.Euler(0f, 0f, degrees);
    }
}
