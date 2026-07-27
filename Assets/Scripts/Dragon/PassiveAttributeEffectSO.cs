using UnityEngine;

// 활성 속성일 때만 값을 반환하는 맵 패시브(예: 얼음=슬로우, 불=화상 수치).
// 소비처(전투 훅)가 아직 없어 값은 보유하되 아무도 읽지 않는 stub이다 - 로드맵 §8.
[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon/Effects/Passive Attribute",
    fileName = "PassiveAttributeEffect")]
public sealed class PassiveAttributeEffectSO : DragonSkillEffectSO
{
    [SerializeField] private DragonType _attribute;
    [SerializeField] private float _value;

    public float GetValueIfActive(DragonType activeAttribute) =>
        activeAttribute == _attribute ? _value : 0f;
}
