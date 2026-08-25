using UnityEngine;

/// <summary>가산 채널 하나에 정수를 더하는 효과. "부화 일수 +1" 같은 것이 여기 온다.</summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Mutators/Effects/Counter",
    fileName = "RME_Counter")]
public sealed class RunCounterEffectSO : RunMutatorEffectSO
{
    [SerializeField]
    [Tooltip("이 효과가 합을 기여하는 채널.")]
    private RunCounterChannel _channel;

    [SerializeField]
    [Tooltip("채널에 더할 값. 0이면 아무 효과가 없다.")]
    private int _delta;

    public override int GetCounterDelta(RunCounterChannel channel)
    {
        if (channel != _channel)
        {
            return base.GetCounterDelta(channel);
        }

        return _delta;
    }

    /// <summary>테스트·에디터 저작 스크립트가 인스펙터 없이 값을 채울 때 쓴다.</summary>
    public void Configure(RunCounterChannel channel, int delta)
    {
        _channel = channel;
        _delta = delta;
    }
}
