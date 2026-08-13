using UnityEngine;

/// <summary>
/// 일정 주기(쿨다운)마다 자신에게 방어막을 재부여하는 특수 행동.
/// </summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Monster/Special Behavior/Regenerating Shield",
    fileName = "RegeneratingShieldBehavior")]
public sealed class RegeneratingShieldBehaviorSO : SpecialBehaviorSO
{
    [Header("Regenerating Shield")]
    [Tooltip("방어막이 재생성되는 주기(초).")]
    [Min(0)]
    [SerializeField] private float _interval;

    [Tooltip("재생성 시 부여할 방어막의 양.")]
    [Min(0)]
    [SerializeField] private float _shieldAmount;

    public override ISpecialBehavior CreateInstance()
    {
        return new RegeneratingShieldBehavior(_interval, _shieldAmount);
    }

    private sealed class RegeneratingShieldBehavior : ISpecialBehavior
    {
        private readonly float _interval;
        private readonly float _shieldAmount;

        private BaseMonster _owner;
        private MonsterShield _shieldComponent;
        private float _timer;

        public RegeneratingShieldBehavior(float interval, float shieldAmount)
        {
            _interval = interval;
            _shieldAmount = shieldAmount;
        }

        public void Initialize(BaseMonster owner)
        {
            _owner = owner;
            _shieldComponent = owner.GetComponent<MonsterShield>();
            // 최초 스폰 시 쿨다운 없이 바로 방어막을 부여할 수 있도록 타이머를 0으로 초기화
            _timer = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_owner == null || _owner.IsDead)
            {
                return;
            }

            _timer -= deltaTime;

            if (_timer <= 0f)
            {
                // 방어막이 깎여서 0 이하(Broken)가 되었다면 기존 데이터를 초기화(Revoke)
                if (_shieldComponent != null && _shieldComponent.IsBroken)
                {
                    _owner.RevokeGrantedShield();
                }

                bool isGranted = _owner.GrantShield(_shieldAmount);

                if (isGranted)
                {
                    // 방어막 부여 성공: 다시 쿨다운 시작
                    _timer = _interval;
                }
                else
                {
                    // 기존 방어막 존재(안 깨짐): 방어막이 깨질 때 즉시 부여될 수 있도록 타이머 0으로 대기
                    _timer = 0f;
                }
            }
        }

        public void Dispose()
        {
            _owner = null;
            _shieldComponent = null;
        }
    }
}
