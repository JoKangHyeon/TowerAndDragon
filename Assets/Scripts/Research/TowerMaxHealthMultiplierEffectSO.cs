using UnityEngine;

// 타워 최대체력 강화(연구 "중갑 타워"). 다른 타워 스탯 효과와 같은 TowerTargetFilter를 쓴다.
//
// 이 효과가 실제로 타워에 닿으려면 씬에 TowerMaxHealthApplier가 있어야 한다 -
// 공격력·공속·사거리처럼 TowerAttack이 pull하는 경로가 아니기 때문이다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Tower Max Health Multiplier",
    fileName = "TowerMaxHealthMultiplierEffect")]
public sealed class TowerMaxHealthMultiplierEffectSO : ResearchEffectSO
{
    [Min(0f)]
    [SerializeField] private float _bonusRatio;

    [Tooltip("대상 타워. 비워 두면(All) 전 타워에 걸린다.")]
    [SerializeField] private TowerTargetFilter _target;

    public float BonusRatio => _bonusRatio;

    public override float GetTowerMaxHealthMultiplierBonus(TowerData towerData)
    {
        return _target.Matches(towerData) ? _bonusRatio : 0f;
    }
}
