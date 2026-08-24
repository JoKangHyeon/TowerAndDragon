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

    private static readonly Color DEFAULT_TRACER_CORE_COLOR = new(1f, 0.95f, 0.75f, 1f);
    private static readonly Color DEFAULT_TRACER_GLOW_COLOR = new(1f, 0.45f, 0.05f, 0.65f);

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

    [Header("Near Tracer")]
    [Tooltip("근거리 명중 뒤 잠깐 남길 공용 트레이서. 비우면 트레이서 없이 동작한다.")]
    [WiringOptional]
    [SerializeField] private GameObject _nearTracerPrefab;

    [Tooltip("트레이서 튜닝값. 공격 투사체 타워 7종이 같은 애셋을 공유한다.\n" +
             "비우면 트레이서가 재생되지 않는다 - 값을 코드 기본값으로 얼버무리면 " +
             "배선 누락이 조용히 묻힌다.")]
    [WiringOptional]
    [SerializeField] private NearTracerTuningSO _nearTracerTuning;

    [Tooltip("트레이서 코어(가운데 흰 선) 색. 프리팹을 공유해도 타워별로 다를 수 있게 여기에 둔다.")]
    [SerializeField] private Color _nearTracerCoreColor = DEFAULT_TRACER_CORE_COLOR;

    [Tooltip("트레이서 글로우(바깥 선) 색. 타워 성격을 이 색으로 구분한다.")]
    [SerializeField] private Color _nearTracerGlowColor = DEFAULT_TRACER_GLOW_COLOR;

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
        Vector3 launchPosition,
        Vector3 hitPosition,
        Vector3 travelDirection,
        GameObject targetObject)
    {
        StopTrails();

        Vector3 visualImpactPosition = ResolveVisualImpactPosition(hitPosition, targetObject);
        PlayNearTracer(launchPosition, visualImpactPosition, travelDirection);

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

    /// <summary>
    /// 근거리 보조 트레이서. <b>방향이 아니라 화면 길이 부족분</b>으로 강도를 정한다.
    ///
    /// v1은 "수직인가"로 대상을 골랐다가 실패했다 - 수직은 원인이 아니라 증상이었다.
    /// 상방 사격이 안 보인 이유는 방향이 아니라 사거리 타원(RADIUS_Y_RATIO 0.5)과
    /// FirePoint 높이가 겹쳐 <b>화면 길이가 횡의 1/2.5로 줄기</b> 때문이고, 그래서 v1의
    /// 48px 게이트에 걸려 정작 문제 구간(상방 최대 199px)은 한 픽셀도 그리지 않았다.
    /// 길이를 직접 재면 그 구간이 자동으로 포함된다. 근거는 작업노트 5-1 · 5-3-1.
    /// </summary>
    private void PlayNearTracer(
        Vector3 launchPosition, Vector3 impactPosition, Vector3 travelDirection)
    {
        if (_nearTracerPrefab == null || _nearTracerTuning == null)
        {
            return;
        }

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return;
        }

        // 줌 아웃 가드. 폭이 줌 독립이 되고 게이트도 화면 px이라, 이것이 없으면 최대 축소에서
        // 모든 사격이 트레이서를 받고 최소 길이 보정이 타워보다 긴 줄을 만든다.
        if (worldCamera.orthographicSize > _nearTracerTuning.MaxOrthographicSize)
        {
            return;
        }

        // WorldToScreenPoint가 돌려주는 것과 같은 픽셀 공간이어야 한다 - Screen.height를 쓰면
        // 레터박스·렌더 텍스처에서 길이 판정과 폭이 서로 다른 기준을 보게 된다.
        float worldPerPixel = 2f * worldCamera.orthographicSize / worldCamera.pixelHeight;

        if (worldPerPixel <= 0f)
        {
            return;
        }

        Vector3 tracerStart = launchPosition;

        // 방향은 종점-시작점이 아니라 진행 방향을 따른다. Ground 정책은 종점을 대상 발밑으로
        // 내리므로, 상방 근거리에서 종점이 발사점보다 아래로 와 선의 부호가 뒤집힌다
        // (v1에서 위로 쏜 트레이서가 아래를 향했다).
        Vector3 direction = travelDirection.sqrMagnitude > Mathf.Epsilon
            ? travelDirection.normalized
            : (impactPosition - tracerStart).normalized;

        float minLengthWorld = _nearTracerTuning.MinScreenLengthPixels * worldPerPixel;
        float lengthAlongTravel = Vector3.Dot(impactPosition - tracerStart, direction);

        if (lengthAlongTravel < minLengthWorld)
        {
            // 끝점(몬스터)은 고정하고 시작점만 뒤로 늘린다 - 시선이 있는 쪽을 건드리지 않는다.
            // 이 연장은 트레이서가 몬스터 쪽에서 밝고 타워 쪽으로 흐려질 때만 자연스럽다
            // (반대 방향이면 타워 스프라이트 안쪽에서 밝은 끝이 시작돼 고장으로 읽힌다).
            float extension = Mathf.Min(
                minLengthWorld, _nearTracerTuning.MaxExtensionWorldUnits);

            tracerStart = impactPosition - direction * extension;
        }

        float screenLengthPixels = (impactPosition - tracerStart).magnitude / worldPerPixel;
        float intensity = _nearTracerTuning.ResolveIntensity(screenLengthPixels);

        // 램프로 바꾸면 사거리 안 사격 대부분이 0보다 큰 강도를 받는다. 이 가드가 없으면
        // "보조 연출"이 상시 연출이 되고 3배속 x 다수 타워에서 풀 대여가 폭증한다.
        if (intensity < _nearTracerTuning.MinEffectiveIntensity)
        {
            return;
        }

        ProjectilePool.PlayTracerForSeconds(
            _nearTracerPrefab,
            tracerStart,
            impactPosition,
            _nearTracerTuning.LifetimeSeconds,
            _nearTracerTuning.GlowHeadWidthPixels * worldPerPixel,
            intensity,
            _nearTracerCoreColor,
            _nearTracerGlowColor);
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
