using UnityEngine;

// 이동속도 배율 상태 - 슬로우(예: 0.7)와 빙결(0)을 같은 타입으로 표현한다.
// 여러 개가 동시에 걸리면 MonsterStatusReceiver가 배율이 가장 작은(가장 강한) 것 하나만 적용한다 -
// 곱으로 중첩하면 순식간에 0에 수렴해 밸런싱이 불가능해진다.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Combat/Status/Move Speed",
    fileName = "MoveSpeedStatus")]
public sealed class MoveSpeedStatusSO : StatusEffectSO
{
    [Range(0f, 1f)]
    [SerializeField] private float _speedMultiplier;

    public float SpeedMultiplier => _speedMultiplier;

    // 둔화든 빙결이든 이동을 제약하므로 전부 군중제어로 취급한다.
    public override bool IsCrowdControl => true;
}
