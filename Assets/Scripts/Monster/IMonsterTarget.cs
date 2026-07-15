using UnityEngine;


/// <summary>
/// 몬스터가 이동 중 탐색하고 공격할 수 있는 대상의 계약
/// </summary>
public interface IMonsterTarget : IAttackTarget
{
    //Tower에서 구현되는 내용
    MonsterTargetType TargetType { get; }
}