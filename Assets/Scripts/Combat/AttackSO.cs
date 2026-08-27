using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 하나의 공격 정의. 사거리·발동 간격과 함께 부여할 효과 목록을 보관한다.
/// 타워와 적이 동일 애셋을 공유할 수 있어, "화염 화살(데미지+화상)" 같은
/// 조합을 코드 수정 없이 기획자가 애셋으로 만든다.
///
/// Execute는 효과 리스트를 대상에 순차 적용할 뿐 상태를 갖지 않는다.
/// 쿨다운 등 런타임 상태는 이 애셋을 사용하는 실행 컴포넌트(MonsterAttack 등)가 보관한다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Attack", fileName = "Attack")]
public class AttackSO : ScriptableObject
{
    [Header("Firing")]
    [SerializeField] private float _range;
    [SerializeField] private float _interval;

    [Header("Effects")]
    [SerializeField] private AttackEffectSO[] _effects;

    [Header("Area")]
    [Min(0f)]
    [SerializeField] private float _areaRadius;

    // 근거리·보스 전용이다. 원거리는 이 필드를 쓰지 않는다 - 투사체 명중 연출은 명중 시점에
    // 투사체가 직접 들고 있어야 해서 MonsterData._projectilePrefab의 ProjectileVisual이 대신 그린다
    // (이미 배선돼 있다). 발화 지점은 MonsterAttack이다 - 광역은 Execute가 대상 수만큼 도는 경로가
    // 있어 "공격 1회"를 아는 것이 MonsterAttack뿐이기 때문에 여기 Execute 안에서는 띄우지 않는다.
    [Header("Hit VFX (근접 공격용 - 투사체가 있으면 ProjectileVisual이 대신 그린다)")]
    [WiringOptional]
    [SerializeField] private GameObject _hitVfxPrefab;

    [Tooltip("명중 이펙트가 스스로 걷히기까지의 시간(초).")]
    [Min(0f)]
    [SerializeField] private float _hitVfxLifetimeSeconds;

    [Tooltip("명중 연출을 놓을 기준점.")]
    [SerializeField] private AttackVfxAnchor _hitVfxAnchor;

    public float Range => _range;
    public float Interval => _interval;
    public GameObject HitVfxPrefab => _hitVfxPrefab;
    public float HitVfxLifetimeSeconds => _hitVfxLifetimeSeconds;
    public AttackVfxAnchor HitVfxAnchor => _hitVfxAnchor;
    public bool HasHitVfx => _hitVfxPrefab != null;

    // 효과 목록을 읽기 전용으로 노출한다 - 빌드모드의 타워 정보 팝업이 피해량(DamageEffectSO.Amount)을
    // 표시하려면 이 목록을 훑어야 한다. 적용은 Execute가 담당하므로 밖에서는 조회만 한다.
    public IReadOnlyList<AttackEffectSO> Effects =>
        _effects ?? System.Array.Empty<AttackEffectSO>();
    public float AreaRadius => _areaRadius;
    public bool HasArea => _areaRadius > 0f;

    /// <summary>
    /// 한 번의 공격이 한 대상에게 주는 피해량 합. 피해 효과가 하나도 없는 공격(회복 오라 등)은
    /// 값이 성립하지 않으므로 false를 돌려준다 - 0을 돌려주면 "공격력 0인 타워"로 읽힌다.
    ///
    /// modifier는 DamageEffectSO.Apply와 같은 순서로 효과마다 적용한다(합산 뒤 한 번이 아니다) -
    /// 가산 보정이 생기면 두 방식의 결과가 달라져 표시값이 실제 피해와 어긋나기 때문이다.
    /// 보정 없는 데이터 원본이 필요하면 ResolvedEnemyStatModifier.Neutral을 넘긴다.
    /// </summary>
    public bool TryGetTotalDamage(in ResolvedEnemyStatModifier modifier, out float total)
    {
        total = 0f;
        bool hasDamage = false;

        foreach (AttackEffectSO effect in Effects)
        {
            if (effect is not DamageEffectSO damage)
            {
                continue;
            }

            total += Mathf.Max(0f, modifier.Apply(damage.Amount));
            hasDamage = true;
        }

        return hasDamage;
    }

    public void Execute(IDamageable target, in AttackContext context)
    {
        if (!HasArea ||
            target is not IAttackTarget impactTarget ||
            impactTarget.TargetTransform == null)
        {
            ApplyEffectsToTarget(target, in context);
            return;
        }
        ApplyEffectsInArea(impactTarget, in context);

    }

    private void ApplyEffectsToTarget(IDamageable target, in AttackContext context)
    {
        // 대공/대지 필터의 유일한 관문. 단일 대상·광역·투사체 명중이 모두 여기로 모이므로
        // 여기 한 곳만 막으면 된다. 특히 ApplyEffectsInArea는 명중 대상을 먼저 집합에 넣기 때문에
        // 후보 순회에만 필터를 걸면 그 대상이 새어나간다.
        // 주의: 피해뿐 아니라 TowerHealAuraEffectSO처럼 대상을 쓰지 않는 효과도 함께 막힌다
        // ("공격 자체가 빗나갔다"는 해석) - 버그가 아니다.
        if (!context.MovementFilter.CanTarget(target))
        {
            return;
        }

        foreach (AttackEffectSO effect in _effects)
        {
            effect.Apply(target, in context);
        }
        ApplyExtraStatuses(target, in context);
    }

    private void ApplyEffectsInArea(
        IAttackTarget impactTarget,
        in AttackContext context
    )
    {
        Vector3 center = impactTarget.TargetTransform.position;
        float radiusY = _areaRadius * IsometricMath.RADIUS_Y_RATIO;

        Collider2D[] candidates = Physics2D.OverlapCircleAll(
            center,
            _areaRadius,
            context.TargetLayers
        );

        var targets = new HashSet<IAttackTarget>
        {
            impactTarget
        };

        foreach (Collider2D candidate in candidates)
        {
            IAttackTarget areaTarget =
                candidate.GetComponentInParent<IAttackTarget>();

            if (areaTarget == null ||
                areaTarget.IsDead ||
                areaTarget.TargetTransform == null)
            {
                continue;
            }

            if (!IsometricMath.IsWithinEllipse(
                    areaTarget.TargetTransform.position,
                    center,
                    _areaRadius,
                    radiusY
            ))
            {
                continue;
            }

            targets.Add(areaTarget);
        }

        foreach (IAttackTarget areaTarget in targets)
        {
            if (!areaTarget.IsDead)
            {
                ApplyEffectsToTarget(areaTarget, in context);
            }
        }
    }

    // 발사 시점(TowerAttack.Fire)이 아니라 명중 시점(여기)에 적용해야 투사체 타이밍과 맞는다 -
    // AttackContext.ExtraStatuses 주석 참고.
    private static void ApplyExtraStatuses(IDamageable target, in AttackContext context)
    {
        if (context.ExtraStatuses == null || !(target is IStatusEffectTarget statusTarget))
        {
            return;
        }

        foreach (StatusEffectSO status in context.ExtraStatuses)
        {
            if (status != null)
            {
                statusTarget.ApplyStatus(status, context.AttackElement);
            }
        }
    }
}
