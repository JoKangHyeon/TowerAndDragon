using UnityEngine;

public interface IMonsterTarget : IDamageable
{
    MonsterTargetType TargetType { get; }
    Transform TargetTransform { get; }
}