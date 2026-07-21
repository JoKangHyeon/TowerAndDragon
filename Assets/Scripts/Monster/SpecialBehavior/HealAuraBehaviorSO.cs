using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일정 간격으로 주변의 살아 있는 아군 몬스터를 회복시키는 특수 행동 설정.
/// 행동 소유자는 치유 대상에서 제외한다.
/// </summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Monster/Special Behavior/Heal Aura",
    fileName = "HealAuraBehavior")]
public sealed class HealAuraBehaviorSO : SpecialBehaviorSO
{
    [Header("Healing")]
    [Min(0)]
    [SerializeField] private float _radius;
    [Min(0)]
    [SerializeField] private float _interval;
    [Min(0)]
    [SerializeField] private float _healAmount;
    [SerializeField] private LayerMask _targetLayers = Physics2D.DefaultRaycastLayers;

    public override ISpecialBehavior CreateInstance()
    {
        return new HealAuraBehavior(_radius, _interval, _healAmount, _targetLayers);
    }

    private sealed class HealAuraBehavior : ISpecialBehavior
    {
        private readonly float _radius;
        private readonly float _interval;
        private readonly float _healAmount;
        private readonly LayerMask _targetLayers;
        private readonly HashSet<BaseMonster> _healedTargets = new();

        private BaseMonster _owner;
        private float _remainingTime;

        public HealAuraBehavior(
            float radius,
            float interval,
            float healAmount,
            LayerMask targetLayers)
        {
            _radius = radius;
            _interval = interval;
            _healAmount = healAmount;
            _targetLayers = targetLayers;
        }

        public void Initialize(BaseMonster owner)
        {
            _owner = owner;
            _remainingTime = _interval;
        }

        public void Tick(float deltaTime)
        {
            if (_owner == null || _owner.IsDead || _radius <= 0 || _healAmount <= 0)
            {
                return;
            }

            _remainingTime -= deltaTime;
            if (_remainingTime > 0)
            {
                return;
            }

            HealNearbyMonsters();
            _remainingTime = _interval;
        }

        public void Dispose()
        {
            _owner = null;
            _healedTargets.Clear();
        }

        private void HealNearbyMonsters()
        {
            _healedTargets.Clear();

            Collider2D[] candidates = Physics2D.OverlapCircleAll(
                _owner.transform.position,
                _radius,
                _targetLayers);

            foreach (Collider2D candidate in candidates)
            {
                BaseMonster target = candidate.GetComponentInParent<BaseMonster>();
                if (target == null ||
                    target == _owner ||
                    target.IsDead ||
                    !_healedTargets.Add(target))
                {
                    continue;
                }

                float previousHealth = target.CurrentHealth;
                target.Heal(_healAmount);
                float healedAmount = target.CurrentHealth - previousHealth;

                if (healedAmount > 0)
                {
                    Debug.Log(
                        $"[HealAuraBehavior] {_owner.name}이(가) {target.name}을(를) {healedAmount}만큼 치유했습니다.",
                        _owner);
                }
            }
        }
    }
}
