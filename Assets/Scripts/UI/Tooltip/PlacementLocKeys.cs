// 배치 미리보기 툴팁이 쓰는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 도메인별 LocKeys 클래스를 두는 기존 관례(ResourceLocKeys, ResearchLocKeys, DragonLocKeys)를 따른다.
//
// 자원 이름은 ResourceData가, 지형 효과 이름은 BuildingEffectIconEntry가 각자 들고 있으므로
// 여기 포함하지 않는다. 줄 서식도 resource_tooltip_row를 그대로 쓴다 - 같은 표시기에 그려지는
// 같은 모양의 줄이라 키를 따로 만들면 두 벌이 어긋난다.
public static class PlacementLocKeys
{
    // 이 숫자가 어떤 가정 위의 값인지 밝히는 각주. "{0}"에 시설 정원이 들어간다.
    public const string FULL_STAFF_NOTE = "placement_yield_full_staff_note";

    // 지을 수는 있지만 그 자리에서 나오는 양이 0인 경우.
    public const string NO_YIELD = "placement_yield_no_yield";
}
