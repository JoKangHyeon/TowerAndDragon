using NUnit.Framework;

// 실제 정산(Factory.OnSettlement)과 배치 미리보기(PlacementYieldEstimator)가 이 규칙 하나를 공유한다.
// 여기서 고정한 값이 곧 "툴팁에 뜬 숫자 = 다음 아침에 들어오는 양"의 보증이다.
public class FactoryYieldRulesTests
{
    private const int FOOTPRINT_YIELD = 20;

    [Test]
    public void Compute_NoStaff_ProducesNothing()
    {
        int produced = FactoryYieldRules.Compute(FOOTPRINT_YIELD, 0f, 1f, 1f);

        Assert.That(produced, Is.EqualTo(0));
    }

    [Test]
    public void Compute_FullStaff_ProducesFootprintYield()
    {
        int produced = FactoryYieldRules.Compute(
            FOOTPRINT_YIELD, FactoryYieldRules.FULL_STAFFING_RATIO, 1f, 1f);

        Assert.That(produced, Is.EqualTo(FOOTPRINT_YIELD));
    }

    // 최소 인구 문턱이 없다 - 절반만 채우면 절반이 나온다.
    [Test]
    public void Compute_HalfStaff_ScalesLinearly()
    {
        int produced = FactoryYieldRules.Compute(FOOTPRINT_YIELD, 0.5f, 1f, 1f);

        Assert.That(produced, Is.EqualTo(10));
    }

    // 충원율이 1을 넘어도 상한은 정원 만충이다.
    [Test]
    public void Compute_OverStaff_ClampsAtFullYield()
    {
        int produced = FactoryYieldRules.Compute(FOOTPRINT_YIELD, 2f, 1f, 1f);

        Assert.That(produced, Is.EqualTo(FOOTPRINT_YIELD));
    }

    // 사막 지형 페널티(-30%)가 그대로 곱해진다.
    [Test]
    public void Compute_TerrainPenalty_ReducesYield()
    {
        int produced = FactoryYieldRules.Compute(
            FOOTPRINT_YIELD, FactoryYieldRules.FULL_STAFFING_RATIO, 1f, 0.7f);

        Assert.That(produced, Is.EqualTo(14));
    }

    // 새끼용 버프와 지형 페널티는 별도 채널이며 둘 다 곱해진다.
    [Test]
    public void Compute_AreaBuffAndTerrainPenalty_MultiplyTogether()
    {
        int produced = FactoryYieldRules.Compute(
            FOOTPRINT_YIELD, FactoryYieldRules.FULL_STAFFING_RATIO, 1.5f, 0.7f);

        Assert.That(produced, Is.EqualTo(21));
    }

    // 반올림은 인구를 곱한 뒤 한 번, 배율을 곱한 뒤 한 번 - 기존 정산 동작과 같아야 한다.
    // 25 * 0.5 = 12.5 -> 12(짝수 반올림), 12 * 0.7 = 8.4 -> 8
    [Test]
    public void Compute_RoundsAfterStaffingAndAgainAfterMultipliers()
    {
        int produced = FactoryYieldRules.Compute(25, 0.5f, 1f, 0.7f);

        Assert.That(produced, Is.EqualTo(8));
    }
}
