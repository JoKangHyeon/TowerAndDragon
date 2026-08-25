/// <summary>"이 뮤테이터를 이 단계로 켰다"는 런타임 선택 상태 하나.
///
/// 세이브 계층의 DTO와 이름을 구분해 둔 것은 의도적이다 - DTO(RunMutatorSelectionDto)는
/// id 문자열을 들고 직렬화되고, 이쪽은 카탈로그 조회를 끝낸 <see cref="RunMutatorSO"/> 참조를 든다.
/// 같은 이름을 쓰면 "조회 전인가 후인가"가 코드에서 안 보인다.</summary>
public readonly struct RunMutatorSelection
{
    public readonly RunMutatorSO Mutator;

    /// <summary>선택된 단계. <b>1-기반</b>이다(0 = 미선택).</summary>
    public readonly int Tier;

    public RunMutatorSelection(RunMutatorSO mutator, int tier)
    {
        Mutator = mutator;
        Tier = tier;
    }
}
