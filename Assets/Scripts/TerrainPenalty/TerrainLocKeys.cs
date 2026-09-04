/// <summary>
/// 지형 이름 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
/// 도메인별 LocKeys 클래스를 두는 기존 관례(ResourceLocKeys, DragonLocKeys)를 따른다.
/// </summary>
public static class TerrainLocKeys
{
    public const string NAME_GRASS = "terrain_name_grass";
    public const string NAME_ROCK = "terrain_name_rock";
    public const string NAME_VOLCANO = "terrain_name_volcano";
    public const string NAME_DESERT = "terrain_name_desert";
    public const string NAME_SNOW = "terrain_name_snow";
    public const string NAME_WATER = "terrain_name_water";
    public const string NAME_ROAD = "terrain_name_road";

    public static string NameLocKey(TerrainType terrain) => terrain switch
    {
        TerrainType.Grass => NAME_GRASS,
        TerrainType.Rock => NAME_ROCK,
        TerrainType.Volcano => NAME_VOLCANO,
        TerrainType.Desert => NAME_DESERT,
        TerrainType.Snow => NAME_SNOW,
        TerrainType.Road => NAME_ROAD,
        TerrainType.Water => NAME_WATER,
        _ => NAME_WATER,
    };
}
