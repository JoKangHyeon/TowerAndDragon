using UnityEngine;

public interface IAttackTarget : IDamageable
{
    Transform TargetTransform { get; }
    GameObject TargetObject { get; }
}
