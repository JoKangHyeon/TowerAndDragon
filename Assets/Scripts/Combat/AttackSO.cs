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

    public float Range => _range;
    public float Interval => _interval;
    public float AreaRadius => _areaRadius;
    public bool HasArea => _areaRadius > 0f;

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
