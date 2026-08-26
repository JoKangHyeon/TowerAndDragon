using NUnit.Framework;

// 실제 정산(ResearchManager.GrantResearchPoints)과 UI 미리보기(PreviewResearchPointsPerDay)가
// 이 규칙 하나를 공유한다. 여기서 고정한 값이 곧 "미리보기 = 다음 밤에 들어오는 RP"의 보증이다.
public class ResearchPointRulesTests
{
    private const int POINTS_PER_POPULATION = 1;

    [Test]
    public void Compute_NeutralMultiplier_ReturnsPopulationTimesPointsPerPopulation()
    {
        int gained = ResearchPointRules.Compute(3, 2, 1f);

        Assert.That(gained, Is.EqualTo(6));
    }

    [Test]
    public void Compute_NoPopulation_GrantsNothing()
    {
        int gained = ResearchPointRules.Compute(0, POINTS_PER_POPULATION, 1f);

        Assert.That(gained, Is.EqualTo(0));
    }

    [Test]
    public void Compute_ZeroMultiplier_GrantsNothing()
    {
        int gained = ResearchPointRules.Compute(5, POINTS_PER_POPULATION, 0f);

        Assert.That(gained, Is.EqualTo(0));
    }

    // 사막 지형 페널티(-30%)가 그대로 곱해진다. 5 * 1 * 0.7 = 3.5 -> 4(짝수 반올림).
    [Test]
    public void Compute_TerrainPenalty_ReducesResearchPoints()
    {
        int gained = ResearchPointRules.Compute(5, POINTS_PER_POPULATION, 0.7f);

        Assert.That(gained, Is.EqualTo(4));
    }

    // 완화 소스(새끼용)가 배율을 1 초과로 올릴 수 있다 - 심화와 반대 방향도 같은 식으로 계산된다.
    [Test]
    public void Compute_MitigatedAboveNeutral_IncreasesResearchPoints()
    {
        int gained = ResearchPointRules.Compute(4, POINTS_PER_POPULATION, 1.5f);

        Assert.That(gained, Is.EqualTo(6));
    }
}
