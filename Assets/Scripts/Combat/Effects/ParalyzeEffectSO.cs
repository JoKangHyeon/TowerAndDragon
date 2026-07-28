using UnityEngine;

// 감전 가능한 대상을 일정 시간 비활성화하는 Effect

[CreateAssetMenu(
    menuName = "TowerAndDragon/Attack Effects/Paralyze",
    fileName = "ParalyzeEffect")]
public sealed class ParalyzeEffectSO : AttackEffectSO
{
    [Tooltip("감전 지속시간(초).")]
    [Min(0f)]
    [SerializeField] private float _duration;

    public float Duration => _duration;

    public override void Apply(IDamageable target, in AttackContext context)
    {
        if (target is not IParalyzable paralyzable ||
            _duration <= 0f)
        {
            return;
        }


        paralyzable.ApplyParalysis(_duration);
    }
}