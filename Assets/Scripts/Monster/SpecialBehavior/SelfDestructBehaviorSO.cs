using UnityEngine;

/// <summary>
/// 사거리(AttackSO.Range) 안에 타워가 들어오면 이동을 멈추고 점화한 뒤,
/// 도화선 시간이 지나면 폭발 범위 안의 타워에 피해를 주고 자신도 사망하는 특수 행동.
///
/// 폭발 피해는 직접 계산하지 않고 MonsterData.Attack(AttackSO)에 위임한다.
/// 덕분에 점령 강화 배수가 일반 공격과 동일하게 적용된다.
/// </summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Monster/Special Behavior/Self Destruct",
    fileName = "SelfDestructBehavior")]
public sealed class SelfDestructBehaviorSO : SpecialBehaviorSO
{
    [Header("Detonation")]
    [Tooltip("점화 후 폭발까지 걸리는 시간(초).")]
    [Min(0)]
    [SerializeField] private float _fuseTime;

    [Tooltip("폭발 피해가 적용되는 반경. 점화 사거리(AttackSO.Range)보다 넓게 잡는다.")]
    [Min(0)]
    [SerializeField] private float _blastRadius;

    public override ISpecialBehavior CreateInstance()
    {
        return new SelfDestructBehavior(_fuseTime, _blastRadius);
    }

    private sealed class SelfDestructBehavior : ISpecialBehavior
    {
        private enum Phase
        {
            Approaching,
            Fusing,
            Detonated
        }

        private readonly float _fuseTime;
        private readonly float _blastRadius;

        private BaseMonster _owner;
        private MonsterAttack _attack;
        private Phase _phase;
        private float _fuseRemaining;

        public SelfDestructBehavior(float fuseTime, float blastRadius)
        {
            _fuseTime = fuseTime;
            _blastRadius = blastRadius;
        }

        public void Initialize(BaseMonster owner)
        {
            _owner = owner;
            _attack = owner != null ? owner.Attack : null;
            _phase = Phase.Approaching;
            _fuseRemaining = _fuseTime;

            // 발사 시점을 이 행동이 통제하므로 자동 공격을 끈다.
            _attack?.DisableAutoAttack();
        }

        public void Tick(float deltaTime)
        {
            if (_owner == null || _owner.IsDead || _attack == null)
            {
                return;
            }

            switch (_phase)
            {
                case Phase.Approaching:
                    UpdateApproaching();
                    break;
                case Phase.Fusing:
                    UpdateFusing(deltaTime);
                    break;
            }
        }

        public void Dispose()
        {
            _owner = null;
            _attack = null;
        }

        private void UpdateApproaching()
        {
            // 사거리 안에 타워가 들어왔거나, 성에 도달했으면 점화한다.
            if (!_attack.HasTargetInRange() && !_owner.HasArrivedAtCastle)
            {
                return;
            }

            _owner.HaltMovement();
            _phase = Phase.Fusing;
            _fuseRemaining = _fuseTime;
        }

        private void UpdateFusing(float deltaTime)
        {
            _fuseRemaining -= deltaTime;

            if (_fuseRemaining > 0)
            {
                return;
            }

            Detonate();
        }

        private void Detonate()
        {
            _phase = Phase.Detonated;

            _attack.ExecuteBlast(_blastRadius);

            Debug.Log($"[SelfDestructBehavior] {_owner.name}이(가) 폭발했습니다.", _owner);

            _owner.Kill();
        }
    }
}