// 자원 예측 툴팁이 쓰는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 전역 Defines 대신 도메인별 LocKeys 클래스를 두는 기존 관례(ResearchLocKeys, DragonLocKeys, SaveLocKeys)를 따른다.
// 자원 이름 키는 ResourceData가 직접 들고 있으므로 여기 포함하지 않는다.
public static class ResourceLocKeys
{
    // 툴팁 한 줄: "{0}" 항목 이름, "{1}" 부호 붙은 수량.
    public const string TOOLTIP_ROW = "resource_tooltip_row";
    public const string TOOLTIP_TOTAL = "resource_tooltip_total";

    public const string SOURCE_PRODUCTION = "resource_forecast_production";
    public const string SOURCE_POPULATION_UPKEEP = "resource_forecast_population_upkeep";
    public const string SOURCE_TERRAIN_UPKEEP = "resource_forecast_terrain_upkeep";

    // 이대로 날을 넘겼을 때 벌어질 일. 자원마다 결과가 다르다.
    public const string WARNING_FOOD = "resource_warning_food";
    public const string WARNING_MATERIAL = "resource_warning_material";
    public const string WARNING_GENERIC = "resource_warning_generic";

    // 부족분 없이 정확히 바닥나는 경우(모자라지는 않지만 다음 날엔 낼 것이 없다).
    public const string WARNING_DEPLETED = "resource_warning_depleted";

    public static string SourceLocKey(ResourceForecastSource source)
    {
        return source switch
        {
            ResourceForecastSource.Production => SOURCE_PRODUCTION,
            ResourceForecastSource.PopulationUpkeep => SOURCE_POPULATION_UPKEEP,
            ResourceForecastSource.TerrainUpkeep => SOURCE_TERRAIN_UPKEEP,
            _ => SOURCE_PRODUCTION,
        };
    }

    // 부족 시 결과가 같은 자원끼리 문구를 공유한다.
    // 식량 = 인구 아사(PopulationUpkeepSystem), 나무·돌 = 시설 인구 배치 해제(TerrainUpkeepSystem).
    public static string WarningLocKey(ResourceType type)
    {
        return type switch
        {
            ResourceType.Food => WARNING_FOOD,
            ResourceType.Wood => WARNING_MATERIAL,
            ResourceType.Stone => WARNING_MATERIAL,
            _ => WARNING_GENERIC,
        };
    }
}
