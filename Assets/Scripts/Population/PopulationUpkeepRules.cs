using System;

/// <summary>
/// 인구 유지비의 실제 정산과 UI 미리보기가 공유하는 순수 계산 규칙이다.
/// </summary>
public static class PopulationUpkeepRules
{
    // 런 배율 채널의 항등원. 이 값이면 뮤테이터가 없는 것과 같으므로 정수 경로로 빠진다.
    private const float NEUTRAL_MULTIPLIER = 1f;

    // float 배율을 double로 넓힐 때 생기는 표현 오차 흡수분. 예를 들어 1.2f는 double로 넓히면
    // 1.2000000476837158...가 되어 인구 50에 곱하면 60.0000024가 되고, 그대로 올림하면 61이 된다
    // (정확히 60이어야 한다). 1e-6은 이 오차(약 2.4e-6)를 못 덮어 실제로 61을 냈다 - 흔한 배율
    // 값(0.x5, 1.x 등)은 대부분 이진수로 정확히 표현되지 않으므로, 인구가 커도 오차를 확실히
    // 삼키도록 여유 있게 잡는다. 올림 직전에 이 값을 빼서 "사실상 정수"인 값이 한 칸 올라가지
    // 않게 한다. 실제 밸런스 값의 최소 유효 단위(예: ×0.05)보다는 훨씬 작아 정당한 올림을
    // 삼키지 않는다.
    private const double CEILING_EPSILON = 1e-3;

    /// <summary>
    /// 런 배율을 반영한 1명당 유효 식량 소모량.
    ///
    /// <b>정수가 아니라 float를 돌려주는 이유</b>: EconomyBalanceData의 1명당 소모량 기본값이 1이라
    /// 1명당 값을 정수로 유지하면 big_appetite 1단계(x1.2)와 2단계(x1.35)가 둘 다 올림에서 2가 되어
    /// <b>두 단계가 완전히 같아진다</b>(2단계가 0점짜리 뮤테이터가 된다). 내림을 고르면 반대로
    /// 1단계가 아무 효과도 없다. 정수 1명당 값으로는 x1.2를 표현할 방법이 아예 없다.
    /// 그래서 1명당 값은 소수로 두고, 인구를 곱한 <b>총액에서 딱 한 번 올림</b>한다
    /// (TerrainUpkeepRules.ResolveFacilityUpkeep이 "시설 단위로 한 번만 반올림"하는 것과 같은 이유다).
    ///
    /// 하한을 1로 클램프하지 않는다 - 기본값 0은 "유지비를 걷지 않는다"는 유효한 설정이고,
    /// 0으로 나누는 것은 소비 지점이 아니라 PopulationUpkeepPreview.PopulationLost가 막는다.
    /// </summary>
    public static float GetEffectiveFoodPerPopulation(int basePerPopulation, float runMultiplier)
    {
        int normalizedBase = Math.Max(0, basePerPopulation);

        // 배율이 항등원이면 곱셈을 타지 않는다 - 뮤테이터가 0개일 때 유지비가 이전과
        // "숫자 하나까지" 같아야 하는데, 그 보장을 부동소수 정확도에 맡기지 않기 위함이다.
        if (runMultiplier == NEUTRAL_MULTIPLIER)
        {
            return normalizedBase;
        }

        return normalizedBase * Math.Max(0f, runMultiplier);
    }

    /// <summary>
    /// 하루에 필요한 식량. 인구 1명당 foodPerPopulation이다.
    /// 자원 예측(ResourceForecast)이 소모량을 집계할 때도 같은 값을 써야 하므로 따로 노출한다.
    ///
    /// 1명당 소모량을 인자로 받는 이유: 이 값은 밸런싱 대상이라 EconomyBalanceData 에셋에 있는데,
    /// 이 클래스는 씬 참조를 가질 수 없는 순수 규칙이므로 호출부가 읽어서 넘긴다.
    /// (런 배율도 같은 이유로 호출부가 RunModifiers.SnapshotOf로 읽어
    /// GetEffectiveFoodPerPopulation을 거친 뒤 넘긴다.)
    ///
    /// 곱셈은 long으로 올려서 계산한다. int로 곱하면 오버플로로 음수가 나오고, 그 음수가
    /// Calculate의 Math.Min을 그대로 통과해 ResourceManager.ConsumeUpTo에 음수로 들어간다.
    /// (Calculate의 projectedFood 합산도 같은 이유로 long이다.)
    /// </summary>
    public static int GetRequiredFood(int population, int foodPerPopulation)
    {
        long required = (long)Math.Max(0, population) * Math.Max(0, foodPerPopulation);

        return required > int.MaxValue ? int.MaxValue : (int)required;
    }

    /// <summary>
    /// 1명당 소모량이 소수일 때의 하루 필요 식량. 총액에서 한 번만 올림한다
    /// (이유는 <see cref="GetEffectiveFoodPerPopulation"/> 주석 참조).
    ///
    /// 올림을 고르는 이유: 내림이면 소수부가 통째로 버려져 인구가 적은 초반에 x1.2가 다시
    /// 무효가 된다. 유지비는 플레이어에게 불리한 쪽으로 정수화하는 것이 뮤테이터의 의도다.
    ///
    /// 곱셈을 double로 하는 이유는 int 오버로드가 long을 쓰는 것과 같다 - float으로 곱하면
    /// 인구가 큰 구간에서 유효 자릿수가 모자라 표시값과 차감액이 어긋난다.
    /// </summary>
    public static int GetRequiredFood(int population, float effectiveFoodPerPopulation)
    {
        double perPopulation = Math.Max(0f, effectiveFoodPerPopulation);
        double required = Math.Ceiling(Math.Max(0, population) * perPopulation - CEILING_EPSILON);

        return required > int.MaxValue ? int.MaxValue : (int)required;
    }

    public static PopulationUpkeepPreview Calculate(
        int population,
        int currentFood,
        int projectedFoodProduction,
        int foodPerPopulation
    )
    {
        return Assemble(
            GetRequiredFood(population, foodPerPopulation),
            currentFood,
            projectedFoodProduction,
            foodPerPopulation
        );
    }

    /// <summary>1명당 소모량이 소수인 경우(런 배율 적용 후). 정수 오버로드와 같은 결과 구조를 낸다.</summary>
    public static PopulationUpkeepPreview Calculate(
        int population,
        int currentFood,
        int projectedFoodProduction,
        float foodPerPopulation
    )
    {
        return Assemble(
            GetRequiredFood(population, foodPerPopulation),
            currentFood,
            projectedFoodProduction,
            foodPerPopulation
        );
    }

    // 필요 식량이 정해진 뒤의 조립은 1명당 소모량의 타입과 무관하다 - 두 오버로드가
    // 서로 다른 결과 구조를 내지 않도록 여기 하나만 둔다.
    private static PopulationUpkeepPreview Assemble(
        int requiredFood,
        int currentFood,
        int projectedFoodProduction,
        float foodPerPopulation
    )
    {
        int normalizedCurrentFood = Math.Max(0, currentFood);
        int normalizedProduction = Math.Max(0, projectedFoodProduction);
        long projectedFood =
            (long)normalizedCurrentFood + normalizedProduction;
        int foodAtSettlement = projectedFood > int.MaxValue
            ? int.MaxValue
            : (int)projectedFood;
        int consumedFood = Math.Min(requiredFood, foodAtSettlement);

        return new PopulationUpkeepPreview(
            requiredFood,
            foodAtSettlement,
            consumedFood,
            foodPerPopulation
        );
    }
}
