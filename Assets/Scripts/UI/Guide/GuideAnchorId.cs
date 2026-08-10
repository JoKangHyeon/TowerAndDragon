/// <summary>
/// 안내가 가리킬 수 있는 대상의 id. 씬에서 GuideAnchor에 지정하고, 안내 데이터는 이 값으로만 대상을 지목한다.
/// 여기 있는 것은 모두 씬에 고정된 오브젝트다 - 런타임에 생성되는 목록 항목(건설 슬롯, 인벤토리 슬롯)은
/// 앵커로 다룰 수 없고 창이 직접 돌려줘야 한다(UI_DragonInventoryWindow.TryGetFirstSlotRect 참고).
/// 단계를 만들면서 필요한 id를 추가한다.
/// </summary>
public enum GuideAnchorId
{
    None = 0,

    // 인게임 HUD (UI_IngameWindow)
    BuildModeButton,
    WorkerModeButton,
    ConquestButton,
    ResearchButton,
    NextNightButton,
    BabyDragonInventoryButton,

    // 건설 패널 카테고리 탭 (UI_BuildModeWindow._filterTabs)
    BuildPanelTowerTab,
    BuildPanelBuildingTab,
    BuildPanelFactoryTab,

    // 인구 배치 패널 (UI_PopulationAllocationWindow)
    PopulationAssignOneButton,
    PopulationUnassignOneButton,
    PopulationAssignAllButton,
    PopulationUnassignAllButton,
    PopulationCloseButton,

    // 건설 패널의 선택 건물 조작 버튼 (UI_BuildModeWindow._activeButtons)
    BuildPanelRemoveButton,
    BuildPanelMoveButton,

    // 새 id는 반드시 뒤에 붙인다 - 중간에 끼우면 이미 만든 단계 에셋이 다른 대상을 가리킨다.

    // 인게임 HUD 표시 영역
    ResourceBar,
    // 인구 패널의 첫 산출 행. 타워면 가동률, 연구소면 연구 포인트가 여기 뜬다 -
    // 건물마다 내용이 달라 이름을 어느 한쪽으로 짓지 않는다.
    PopulationOutputRow,
    DragonSkillButton,

    // 점령 패널 (UI_ConquestWindow)
    // 몬스터 강화 효과는 앵커를 두지 않는다 - 슬롯이 런타임 생성이라 씬에서 잡을 수 없고,
    // 적용 항목이 없는 청크에서는 컨테이너가 비어 강조할 사각형 자체가 사라진다.
    ConquestPanelDuration,
    ConquestPanelCost,
    ConquestPanelClaimButton,

    // 새끼용 인벤토리 탭 (UI_DragonInventoryWindow)
    DragonInventoryEggTab,
    DragonInventoryDragonTab,

    // 새끼용 관리 패널 (UI_BabyDragonManageWindow)
    BabyDragonSkillTreeButton,
    BabyDragonRelocateButton,
    BabyDragonRemoveButton,

    // 메인 성 패널 (UI_MainCastleWindow)
    MainCastleAttributeList,
    MainCastleChangeAttributeButton,
    MainCastleSkillTreeButton,

    // 새끼용 관리 패널의 정보 행 (UI_BabyDragonManageWindow)
    // 행마다 따로 둔다 - 한 id를 두 오브젝트에 붙이면 GuideAnchorRegistry가 나중 것으로 덮어써
    // 둘 중 하나만 강조된다. 부모(InfoZone)에 붙이는 방법은 상태·먹이 행까지 함께 덮어 쓸 수 없다.
    BabyDragonFeedRow,
    BabyDragonBuffRadiusRow,
    BabyDragonBuffMultiplierRow,
    BabyDragonStatusRow,

    // 용 창 새끼용 탭 안의 두 목록 (UI_DragonWindow / Panel_BabyDragon)
    // 슬롯 하나가 아니라 목록 영역을 가리킨다 - 알이 없거나 아직 안 그려진 순간에도 가리킬 곳이 있고,
    // "여기가 알 인벤토리다"라는 설명에는 슬롯보다 영역이 맞다.
    DragonEggInventoryList,
    BabyDragonInventoryList,

    // 용 창 어미용 탭 버튼 (UI_DragonWindow / TabMenu/Button_MotherDragon)
    // 새끼용 관리 패널의 SKILL TREE 버튼은 용 창을 열 뿐이고 탭은 마지막에 본 것이 유지되므로,
    // 스킬 트리를 보여주려면 이 탭을 직접 누르게 해야 한다.
    DragonWindowMotherTab,

    // 오늘 밤 몬스터가 지나갈 경로가 보이는 화면 영역. UI가 아니라 월드를 가리키는 유일한 앵커다 -
    // 딤 구멍은 패널 사이의 빈 칸이라 그 자리에는 경로선이 그대로 비친다.
    // 경로 모양대로 뚫을 수는 없으므로 그 일대를 덮는 빈 사각형을 씬에 두고 그것을 가리킨다.
    MonsterPathArea,

    // 일일 퀘스트 목록 창. 패널 루트가 아니라 실제로 보이는 창(Window)에 붙인다 -
    // 루트는 높이가 0이라 그것을 가리키면 딤 구멍이 납작하게 뚫린다.
    TutorialObjectivePanel,
}
