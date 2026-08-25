using System;

/// <summary>선택된 뮤테이터 전부를 합성한 런 단위 수정치. 런 시작 시 한 번 계산되고 바뀌지 않는다.
///
/// <b>채널마다 명명 필드를 나열하지 않는 이유</b> - 설계서 §3.4는 MultiplierChunkYield 같은
/// 필드를 채널마다 하나씩 두었다. 그러면 채널을 하나 늘릴 때 enum과 필드 <b>두 곳</b>을 고쳐야 하고,
/// 한쪽을 빠뜨리면 효과 SO는 정상인데 스냅샷이 그 값을 옮기지 않아 <b>컴파일 에러 없이 조용히
/// 무효가 된다.</b> 배열 기반이면 enum 멤버를 추가하는 것만으로 길이가 따라 늘어난다.
///
/// 배열을 외부에 노출하지 않는 이유는 그 참조로 항등원 계약(곱연산 1 / 가산 0)이 깨질 수 있기
/// 때문이다. 읽기는 세 게터로만 한다.</summary>
public readonly struct RunModifierSnapshot
{
    // 곱연산 채널의 항등원. 배열 범위를 벗어난 조회에도 이 값을 돌려준다.
    private const float MULTIPLIER_IDENTITY = 1f;

    // 가산 채널의 항등원.
    private const int COUNTER_IDENTITY = 0;

    private static readonly int MULTIPLIER_CHANNEL_COUNT =
        Enum.GetValues(typeof(RunModifierChannel)).Length;

    private static readonly int COUNTER_CHANNEL_COUNT =
        Enum.GetValues(typeof(RunCounterChannel)).Length;

    private readonly float[] _multipliers;
    private readonly int[] _counters;
    private readonly RunRuleFlag _rules;

    /// <summary>뮤테이터가 하나도 없는 상태. 모든 채널이 항등원이라 표준 모드와 결과가 같다.
    /// default(RunModifierSnapshot)도 배열이 null이라 같은 값을 돌려주지만, 의도를 드러내려면
    /// 이 프로퍼티를 쓴다.</summary>
    public static RunModifierSnapshot Neutral => default;

    public RunModifierSnapshot(float[] multipliers, int[] counters, RunRuleFlag rules)
    {
        _multipliers = multipliers;
        _counters = counters;
        _rules = rules;
    }

    /// <summary>합성기가 채널 배열을 만들 때 쓰는 항등원으로 찬 배열. 리졸버 전용.</summary>
    public static float[] CreateNeutralMultipliers()
    {
        float[] multipliers = new float[MULTIPLIER_CHANNEL_COUNT];

        for (int i = 0; i < multipliers.Length; i++)
        {
            multipliers[i] = MULTIPLIER_IDENTITY;
        }

        return multipliers;
    }

    /// <summary>가산 채널은 항등원이 0이라 new int[]가 곧 중립 상태다. 대칭을 위해 함께 둔다.</summary>
    public static int[] CreateNeutralCounters()
    {
        return new int[COUNTER_CHANNEL_COUNT];
    }

    /// <summary>곱연산 채널 값. 미선택·범위 밖이면 1을 돌려준다.</summary>
    public float GetMultiplier(RunModifierChannel channel)
    {
        int index = (int)channel;

        if (_multipliers == null || index < 0 || index >= _multipliers.Length)
        {
            return MULTIPLIER_IDENTITY;
        }

        return _multipliers[index];
    }

    /// <summary>가산 채널 값. 미선택·범위 밖이면 0을 돌려준다.</summary>
    public int GetCounter(RunCounterChannel channel)
    {
        int index = (int)channel;

        if (_counters == null || index < 0 || index >= _counters.Length)
        {
            return COUNTER_IDENTITY;
        }

        return _counters[index];
    }

    /// <summary>규칙 플래그가 켜져 있는지. None을 넘기면 항상 false다(켜짐 없음).</summary>
    public bool HasRule(RunRuleFlag flag)
    {
        if (flag == RunRuleFlag.None)
        {
            return false;
        }

        return (_rules & flag) == flag;
    }
}
