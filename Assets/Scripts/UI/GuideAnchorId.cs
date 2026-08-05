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
}
