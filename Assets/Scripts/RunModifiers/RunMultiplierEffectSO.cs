using UnityEngine;

/// <summary>곱연산 채널 하나에 배율을 곱하는 효과. 수치 계열 뮤테이터의 대부분이 이것이다.</summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Mutators/Effects/Multiplier",
    fileName = "RME_Multiplier")]
public sealed class RunMultiplierEffectSO : RunMutatorEffectSO
{
    [SerializeField]
    [Tooltip("이 효과가 곱을 기여하는 채널.")]
    private RunModifierChannel _channel;

    [SerializeField]
    [Tooltip("채널에 곱할 배율. 1이면 아무 효과가 없다.")]
    private float _multiplier = 1f;

    public override float GetMultiplier(RunModifierChannel channel)
    {
        if (channel != _channel)
        {
            return base.GetMultiplier(channel);
        }

        return _multiplier;
    }

    /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
    public void Configure(RunModifierChannel channel, float multiplier)
    {
        _channel = channel;
        _multiplier = multiplier;
    }
}
