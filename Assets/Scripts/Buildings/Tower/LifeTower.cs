using UnityEngine;

/// <summary>
/// 생명 타워(Life Tower).
/// 몬스터를 향한 기본 공격(TowerAttack)은 비활성화하고, TowerAllyHealer를 통해 아군을 타겟팅하여 회복시킨다.
/// </summary>
[RequireComponent(typeof(TowerAllyHealer))]
public class LifeTower : Tower
{
    // 적을 공격하지 않도록 기본 공격 시스템(TowerAttack)의 발사를 막는다.
    protected override bool CanAttackInCurrentMode => false;
}
