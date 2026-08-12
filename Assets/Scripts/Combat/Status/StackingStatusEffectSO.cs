using UnityEngine;

// 같은 대상에 반복 적용해 스택을 쌓다가, 임계치에 도달하면 다른 상태를 부여하고 스택을 비운다.
// 얼음 새끼용의 "3스택 → 3초 빙결"이 이 형태다.
// 상속받은 DurationSeconds는 "부여 지속시간"이 아니라 "쌓인 스택이 유지되는 시간"으로 쓴다 -
// 이게 없으면 아무리 드물게 때려도 결국 반드시 임계치에 도달해 밸런싱이 불가능해진다.

[CreateAssetMenu(
    menuName = "TowerAndDragon/Combat/Status/Stacking",
    fileName = "StackingStatus" 
)]
public sealed class StackingStatusEffectSO : StatusEffectSO
{
    [Tooltip("이 스택 수에 도달하면 TriggeredStatus를 부여하고 스택을 비운다")]
    [Min(1f)]
    [SerializeField] private int _stacksToTrigger;

    [Tooltip("임계치 도달 시 부여할 상타 (빙결). 스택 상태 중복 금지")]
    [SerializeField] private StatusEffectSO _triggeredStatus;

    public int StacksToTrigger => _stacksToTrigger;
    public StatusEffectSO TriggeredStatus => _triggeredStatus;

    // 스택 자체는 아무 제약도 걸지 않지만, 임계치에서 부여할 상태가 군중제어면 이 스택도 군중제어로 본다 -
    // 면역 대상에게는 어차피 발동해도 무시되므로 스택을 쌓는 것부터 막는다.
    // 스택 상태를 다시 가리키는 설정은 TriggerStack과 같은 규칙으로 제외해 무한 재귀를 막는다.
    // null 판정은 패턴(is not null)이 아니라 Unity의 == 오버로드를 쓴다 - 에셋이 삭제돼 참조가
    // 끊기면 패턴 매칭은 "null 아님"으로 보고 그대로 역참조하기 때문이다.
    public override bool IsCrowdControl =>
        _triggeredStatus != null &&
        _triggeredStatus is not StackingStatusEffectSO &&
        _triggeredStatus.IsCrowdControl;
}