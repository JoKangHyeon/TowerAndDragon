using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


[CreateAssetMenu(
    menuName = "TowerAndDragon/Attack Effects/Tower Heal Aura",
    fileName = "TowerHealAuraEffect")]
public sealed class TowerHealAuraEffectSO : AttackEffectSO
{
    private const float DEFAULT_HEAL_VFX_LIFETIME_SECONDS = 1f;

    [Min(0f)] [SerializeField] private float _radius;
    [Min(0f)] [SerializeField] private float _healAmount;
    [SerializeField] private LayerMask _towerLayers = Physics2D.DefaultRaycastLayers;

    [Tooltip("시전자 자신도 회복 대상에 포함할지.")]
    [SerializeField] private bool _healsSelf = true;

    [Header("VFX")]
    [WiringOptional]
    [Tooltip("공격 명중 이펙트와 별개로 시전자 위치에 재생할 회복 파동. 비우면 생략한다.")]
    [SerializeField] private GameObject _healVfxPrefab;

    [Min(0f)]
    [SerializeField]
    private float _healVfxLifetimeSeconds =
        DEFAULT_HEAL_VFX_LIFETIME_SECONDS;

    // target(피격 몬스터)은 쓰지 않는다 - 이 효과는 "공격이 발생했다"는 신호만 이용해
    // 시전자 주변 아군 타워를 회복시킨다.
    public override void Apply(IDamageable target, in AttackContext context)
    {
        /* context.Source 중심 타원 스캔 → Tower.Heal */ 
        if (_radius <= 0f || _healAmount <= 0f || context.Source == null)
        {
            return;
        }

        Tower owner = context.Source.GetComponent<Tower>();
        // 다른 사거리·버프 반경 판정(TowerAttack·TowerAllyHealer 등)과 같은 기준점(타일 표면 중앙)을
        // 써야 이 회복 범위가 그 원들과 어긋나지 않는다. owner가 없는 예외적인 호출부를 위해
        // transform.position을 대체값으로 남긴다.
        Vector3 center = owner != null ? owner.GroundWorldPosition : context.Source.transform.position;

        if (_healVfxPrefab != null)
        {
            ProjectilePool.PlayForSeconds(
                _healVfxPrefab,
                center,
                Quaternion.identity,
                _healVfxLifetimeSeconds);
        }

        float radiusY = _radius * IsometricMath.RADIUS_Y_RATIO;

        Collider2D[] candidates = Physics2D.OverlapCircleAll(center, _radius, _towerLayers);
        var healedTowers = new HashSet<Tower>();

        foreach (Collider2D candidate in candidates)
        {
            Tower tower = candidate.GetComponentInParent<Tower>();

            if (tower == null || tower.IsDead || !healedTowers.Add(tower))
            {
                continue;
            }

            if (!_healsSelf && tower == owner)
            {
                continue;
            }

            if (!IsometricMath.IsWithinEllipse(
                    tower.GroundWorldPosition,
                    center,
                    _radius,
                    radiusY))
            {
                continue;
            }

            tower.Heal(_healAmount);
        }
    }
}
