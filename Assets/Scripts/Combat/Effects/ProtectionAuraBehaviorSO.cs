using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범위 내 아군 몬스터에게 방어막을 부여하는 특수 행동 설정(보호형).
/// 범위를 벗어나면 부여했던 방어막을 회수한다. 한 번 부여받은 몬스터는
/// 이후 범위에 다시 들어와도 재부여받지 않는다.
/// </summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Monster/Special Behavior/Protection Aura",
    fileName = "ProtectionAuraBehavior")]
public sealed class ProtectionAuraBehaviorSO : SpecialBehaviorSO
{
    [Header("Protection")]
    [Min(0)]
    [SerializeField] private float _radius;
    [Min(0)]
    [SerializeField] private float _scanInterval;
    [Min(0)]
    [SerializeField] private float _shieldAmount;
    [SerializeField] private LayerMask _targetLayers = Physics2D.DefaultRaycastLayers;

    public override ISpecialBehavior CreateInstance()
    {
        return new ProtectionAuraBehavior(_radius, _scanInterval, _shieldAmount, _targetLayers);
    }

    private sealed class ProtectionAuraBehavior : ISpecialBehavior
    {
        private readonly float _radius;
        private readonly float _scanInterval;
        private readonly float _shieldAmount;
        private readonly LayerMask _targetLayers;

        // 한 번이라도 부여를 시도한 대상 - 평생 유지, 재부여 금지.
        private readonly HashSet<BaseMonster> _grantedOnce = new();
        // 지금 범위 안에서 이 오라가 보호막을 걸어준 대상 - 이탈 감지용.
        private readonly HashSet<BaseMonster> _currentlyProtected = new();
        private readonly HashSet<BaseMonster> _scanBuffer = new();

        private BaseMonster _owner;
        private float _remainingTime;

        public ProtectionAuraBehavior(
            float radius,
            float scanInterval,
            float shieldAmount,
            LayerMask targetLayers)
        {
            _radius = radius;
            _scanInterval = scanInterval;
            _shieldAmount = shieldAmount;
            _targetLayers = targetLayers;
        }

        public void Initialize(BaseMonster owner)
        {
            _owner = owner;
            _remainingTime = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_owner == null || _owner.IsDead || _radius <= 0 || _shieldAmount <= 0)
            {
                return;
            }

            _remainingTime -= deltaTime;
            if (_remainingTime > 0)
            {
                return;
            }

            ScanAndUpdateProtection();
            _remainingTime = _scanInterval;
        }

        public void Dispose()
        {
            foreach (BaseMonster target in _currentlyProtected)
            {
                target?.RevokeGrantedShield();
            }
            _owner = null;
            _grantedOnce.Clear();
            _currentlyProtected.Clear();
            _scanBuffer.Clear();
        }

        private void ScanAndUpdateProtection()
        {
            AuraTargetScanner.FindNearbyMonsters(_owner, _radius, _targetLayers, _scanBuffer);

            foreach (BaseMonster target in _scanBuffer)
            {
                if (!_grantedOnce.Contains(target))
                {
                    Grant(target);
                }
            }

            RevokeOutOfRangeTargets();
        }

        private void Grant(BaseMonster target)
        {
            _grantedOnce.Add(target);

            if (!target.GrantShield(_shieldAmount))
            {
                // 이미 자체 방어막을 가진 대상 - 부여하지 않되, 다시 검사하지 않도록 _grantedOnce는 유지.
                return;
            }

            _currentlyProtected.Add(target);

            Debug.Log(
                $"[ProtectionAuraBehavior] {_owner.name}이(가) {target.name}에게 보호막을 부여했습니다.",
                _owner);
        }

        private void RevokeOutOfRangeTargets()
        {
            _currentlyProtected.RemoveWhere(target =>
            {
                if (target != null && !target.IsDead && _scanBuffer.Contains(target))
                {
                    return false;
                }

                target?.RevokeGrantedShield();
                return true;
            });
        }
    }
}
