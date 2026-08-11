/// <summary>
/// 새끼용 지역 버프의 순수 공식. MonoBehaviour를 모르므로 EditMode에서 그대로 검증할 수 있다
/// (BabyDragonFeedFormula와 같은 관례).
///
/// 실제 적용값과 UI 표시값이 갈리지 않도록 배율 계산은 여기 하나만 쓴다 - 관리창이 데이터 원본
/// (BuffYieldMultiplier)을 직접 읽어 혈족 강화가 빠진 숫자를 보여주던 문제를 이 한 곳으로 모은다.
/// </summary>
public static class BabyDragonBuffFormula
{
    /// <summary>혈족 강화를 해금하지 않았을 때의 보너스 비율.</summary>
    public const float NO_KIN_BONUS_RATIO = 0f;

    /// <summary>생산량에 실제로 곱해지는 배율. 혈족 강화(어미용 트리)가 곱연산으로 얹힌다.</summary>
    public static float ResolveYieldMultiplier(BabyDragonData data, float kinBonusRatio)
    {
        if (data == null)
        {
            return Factory.NEUTRAL_YIELD_MULTIPLIER;
        }

        return data.BuffYieldMultiplier * (1f + kinBonusRatio);
    }

    /// <summary>
    /// 생산량 버프를 실제로 내는지. 반경이나 대상 자원이 없으면 배율이 얼마든 아무 시설에도 곱해지지 않는다
    /// (불·얼음·시간은 대상 자원이 None이고 건설 해제·페널티 완화 같은 다른 지역 효과를 쓴다).
    /// </summary>
    public static bool HasYieldBuff(BabyDragonData data) =>
        data != null &&
        data.BuffRadius > 0f &&
        data.BuffTargetResources != ResourceType.None;
}
