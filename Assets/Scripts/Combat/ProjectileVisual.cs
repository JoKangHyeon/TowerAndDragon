using UnityEngine;
using UnityEngine.Rendering;

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
    private const float DEFAULT_TRAJECTORY_TIME_SECONDS = 0.18f;
    private const float DEFAULT_TRAJECTORY_START_WIDTH = 0.14f;
    private const float DEFAULT_TRAJECTORY_END_WIDTH = 0.01f;
    private const float DEFAULT_TRAJECTORY_MIN_VERTEX_DISTANCE = 0.04f;
    private const float DEFAULT_TRAJECTORY_END_ALPHA = 0.05f;
    private const int PROJECTILE_SORTING_ORDER = 20;
    private const string PROJECTILE_SORTING_LAYER = "Projectile";
    private const string TRAJECTORY_OBJECT_NAME = "TrajectoryTrail";
    private const string URP_PARTICLE_UNLIT_SHADER =
        "Universal Render Pipeline/Particles/Unlit";
    private const string FALLBACK_SPRITE_SHADER = "Sprites/Default";

    private static Material _fallbackTrajectoryMaterial;

    [Tooltip("날아가는 동안 재생할 궤적 파티클. **비워 두면 자식 파티클 전부를 쓴다** - " +
             "일부만 골라 쓰고 싶을 때만 채운다.")]
    [WiringOptional]
    [SerializeField] private ParticleSystem[] _trailParticles;

    [Header("Trajectory Trail")]
    [Tooltip("파티클 코어가 이동하는 동안 뒤에 짧은 감쇠 궤적을 남긴다.")]
    [SerializeField] private bool _trajectoryTrailEnabled;

    [Tooltip("궤적이 화면에 남는 시간(초). 속도 10 기준 0.18초면 약 1.8 월드 유닛이다.")]
    [Min(0f)]
    [SerializeField] private float _trajectoryTimeSeconds =
        DEFAULT_TRAJECTORY_TIME_SECONDS;

    [Tooltip("투사체 머리 쪽 궤적 폭.")]
    [Min(0f)]
    [SerializeField] private float _trajectoryStartWidth =
        DEFAULT_TRAJECTORY_START_WIDTH;

    [Tooltip("궤적 꼬리 끝 폭.")]
    [Min(0f)]
    [SerializeField] private float _trajectoryEndWidth =
        DEFAULT_TRAJECTORY_END_WIDTH;

    [Tooltip("궤적 색. 알파는 머리 쪽 불투명도이며 꼬리로 갈수록 사라진다.")]
    [SerializeField] private Color _trajectoryColor = Color.white;

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

    private TrailRenderer _trajectoryTrail;

    /// <summary>발사 직후. 풀에서 꺼낸 인스턴스는 지난번 입자를 들고 있으므로 되감아 재생한다.</summary>
    public void OnLaunched(Vector3 travelDirection)
    {
        PlayTrajectoryTrail();
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

        if (_trajectoryTrailEnabled)
        {
            _trajectoryTrail = CreateTrajectoryTrail();
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
        StopTrajectoryTrail();
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
            ? new Vector3(bounds.center.x, ResolveGroundY(targetObject), hitPosition.z)
            : new Vector3(bounds.center.x, bounds.center.y, hitPosition.z);
    }

    // 발밑은 스프라이트 경계로 구하면 안 된다. `bounds.min.y`는 스프라이트 사각형의 밑변이고,
    // PixelWorld 시트는 발 위치에 커스텀 피벗을 두기 때문에 그 아래로 셀 높이의 18~25%가 남는다.
    // 보스 1(GolemGian_194, 셀 194 / PPU 32 / 피벗 0.18)은 사각형 밑변이 피벗선보다 1.09 아래고,
    // 실측한 시각 오차는 0.82 월드 유닛이었다.
    // 이동 컴포넌트의 지면 좌표가 이 프로젝트의 발밑 기준이며 지형 고저차 보정도 들어 있다.
    private static float ResolveGroundY(GameObject targetObject)
    {
        MonsterMovement movement = targetObject.GetComponent<MonsterMovement>();

        return movement != null
            ? movement.GroundPlanePosition.y
            : targetObject.transform.position.y;
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

    private TrailRenderer CreateTrajectoryTrail()
    {
        GameObject trailObject = new GameObject(TRAJECTORY_OBJECT_NAME);
        trailObject.transform.SetParent(transform, false);

        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = ResolveTrajectoryMaterial();
        trail.time = _trajectoryTimeSeconds;
        trail.startWidth = _trajectoryStartWidth;
        trail.endWidth = _trajectoryEndWidth;
        trail.minVertexDistance = DEFAULT_TRAJECTORY_MIN_VERTEX_DISTANCE;
        trail.startColor = _trajectoryColor;
        trail.endColor = new Color(
            _trajectoryColor.r,
            _trajectoryColor.g,
            _trajectoryColor.b,
            DEFAULT_TRAJECTORY_END_ALPHA);
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.lightProbeUsage = LightProbeUsage.Off;
        trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
        trail.generateLightingData = false;
        trail.sortingLayerName = PROJECTILE_SORTING_LAYER;
        trail.sortingOrder = PROJECTILE_SORTING_ORDER;
        trail.emitting = false;
        trail.Clear();

        return trail;
    }

    private Material ResolveTrajectoryMaterial()
    {
        ParticleSystemRenderer particleRenderer =
            GetComponentInChildren<ParticleSystemRenderer>(true);

        if (particleRenderer != null && particleRenderer.sharedMaterial != null)
        {
            return particleRenderer.sharedMaterial;
        }

        if (_fallbackTrajectoryMaterial != null)
        {
            return _fallbackTrajectoryMaterial;
        }

        Shader shader = Shader.Find(URP_PARTICLE_UNLIT_SHADER);
        if (shader == null)
        {
            shader = Shader.Find(FALLBACK_SPRITE_SHADER);
        }

        if (shader == null)
        {
            return null;
        }

        _fallbackTrajectoryMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
        };

        return _fallbackTrajectoryMaterial;
    }

    private void PlayTrajectoryTrail()
    {
        if (_trajectoryTrail == null)
        {
            return;
        }

        _trajectoryTrail.emitting = false;
        _trajectoryTrail.Clear();
        _trajectoryTrail.emitting = true;
    }

    private void StopTrajectoryTrail()
    {
        if (_trajectoryTrail == null)
        {
            return;
        }

        _trajectoryTrail.emitting = false;
        _trajectoryTrail.Clear();
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
