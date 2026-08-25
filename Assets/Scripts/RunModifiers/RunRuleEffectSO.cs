using UnityEngine;

/// <summary>코드 분기를 바꾸는 규칙 플래그를 켜는 효과. 규칙 계열 뮤테이터가 이것을 쓴다.
/// 플래그 enum이므로 한 에셋이 여러 규칙을 동시에 켤 수도 있다.</summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Mutators/Effects/Rule",
    fileName = "RME_Rule")]
public sealed class RunRuleEffectSO : RunMutatorEffectSO
{
    [SerializeField]
    [Tooltip("이 효과가 켜는 규칙. 여러 개를 동시에 켤 수 있다.")]
    private RunRuleFlag _rules = RunRuleFlag.None;

    public override RunRuleFlag GetRules()
    {
        return _rules;
    }

    /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
    public void Configure(RunRuleFlag rules)
    {
        _rules = rules;
    }
}
