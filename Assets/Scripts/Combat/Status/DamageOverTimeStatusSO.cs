using UnityEngine;

// 지속 피해 상태(화상 등). 같은 StatusId를 가진 상태는 지속시간만 갱신되고,
// 다른 StatusId끼리는 병렬로 누적된다(MonsterStatusReceiver 참고).
[CreateAssetMenu(
    menuName = "TowerAndDragon/Combat/Status/Damage Over Time",
    fileName = "DamageOverTimeStatus")]
public sealed class DamageOverTimeStatusSO : StatusEffectSO
{
    [Min(0f)]
    [SerializeField] private float _damagePerTick;

    [Min(0.01f)]
    [SerializeField] private float _tickIntervalSeconds;

    public float DamagePerTick => _damagePerTick;
    public float TickIntervalSeconds => _tickIntervalSeconds;
}
