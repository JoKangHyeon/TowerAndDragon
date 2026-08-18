/// <summary>
/// 행동형 단계를 끝내는 조건. 조건 자체는 이벤트 훅이라 본질적으로 코드이고,
/// 여기서는 "어느 훅을 볼지"만 데이터로 고른다. 실제 구독은 TutorialRunner가 한다.
/// </summary>
public enum TutorialConditionType
{
    // 설명형 단계용. 행동형에 이 값이 남아 있으면 단계가 스스로 넘어가지 못한다.
    None = 0,

    // UIManager.OpenExclusive - 버튼·단축키 어느 경로로 열어도 여기를 지난다.
    ExclusiveModeOpened,

    // BuildingPlacementController.BuildingToPlaceChanged - 무엇을 지을지 골라 고스트가 커서를 따라붙은 시점.
    // "고르기"와 "타일 찍기"를 나눠야 배치 단계에서 딤을 걷을 수 있다.
    BuildingSelectedForPlacement,

    // GridMap.OnBuildingAdded - 성 등록으로도 발행되므로 종류를 반드시 지정한다.
    BuildingConstructed,

    // PopulationManager.PopulationChanged - 단계 진입 시점보다 배치 인구가 그만큼 늘어나면 충족.
    // 총합으로 보면 성이나 기존 시설에 이미 인구가 있을 때 진입 즉시 통과해버린다.
    PopulationAssigned,

    // BuildingPlacementController.SelectedBuildingChanged - 그리드의 건물을 클릭해 골랐다.
    // 인구 패널이 열리는 것과 같은 뜻이다(그 창 자체엔 열림 훅이 없다).
    // 새 값은 뒤에 붙인다 - 중간에 끼우면 이미 만든 단계 에셋의 조건이 다른 것으로 바뀐다.
    BuildingSelectedOnGrid,

    // 단계 진입 시점보다 배치 인구가 그만큼 줄었다. 회수 버튼을 눌러보게 하는 단계용.
    PopulationUnassigned,

    // 지금 고른 건물이 정원을 다 채웠다. "전부 배치" 버튼을 눌러보게 하는 단계용 -
    // 증가분으로 보면 +1을 여러 번 눌러도 통과해 안내와 어긋난다.
    SelectedBuildingFullyStaffed,

    // 고른 건물이 없어졌다(패널 닫기 = Deselect). 창에 닫힘 훅이 없어 선택 해제로 판정한다.
    BuildingDeselectedOnGrid,

    // 건설 패널에서 이 단계가 가리키는 탭이 골라졌다. 어느 탭인지는 단계의 앵커가 곧 답이므로
    // 별도 파라미터를 두지 않는다 - 가리키는 것과 눌러야 하는 것이 어긋날 수 없다.
    BuildPanelTabSelected,

    // ConquestManager.OnExpeditionSent - 점령 파병을 보냈다.
    // 점령 완료는 며칠 걸려 1일차에 끝나지 않으므로 파병 시점을 조건으로 삼는다.
    ExpeditionSent,

    // UIManager.ExclusiveModeClosed - 안내가 지목한 창을 닫았다(ESC·버튼 어느 쪽이든).
    // "다 봤으면 닫으세요" 단계용. 어떤 창인지는 ExclusiveModeOpened와 같이 TargetMode로 지정한다.
    ExclusiveModeClosed,

    // GridMap.OnBuildingRemoving - 건물을 철거했다. 환급을 보여주는 단계용.
    BuildingRemoved,

    // GridMap.OnBuildingMoved - 건물을 같은 인스턴스로 옮겼다. 새끼용 재배치 단계용.
    BuildingMoved,

    // ResearchManager.NodeCompleted - 연구를 하나 해금했다. 어느 노드인지는 묻지 않는다 -
    // 안내는 "연구하는 법"을 알려주는 것이지 특정 노드를 강요하지 않는다.
    ResearchNodeCompleted,

    // UI_ConquestWindow.ChunkSelected - 점령지를 골라 패널이 열렸다.
    // 어느 땅인지는 묻지 않는다 - 안내는 고르는 법이지 목표를 지정하지 않는다.
    ConquestChunkSelected,

    // UI_DragonInventoryWindow.OnTabDisplayed - 새끼용 인벤토리에서 용 탭이 보이게 됐다.
    // 알 탭은 기본값이라 "눌러서 바꾸라"고 시킬 것이 없어 용 탭만 조건으로 둔다.
    DragonInventoryDragonTabSelected,

    // 같은 OnTabDisplayed의 반대쪽 - 어미용 탭이 보이게 됐다.
    // 탭 선택은 창을 닫아도 유지되므로, 새끼용 탭을 본 뒤 스킬 트리로 보내려면 이 조건이 필요하다.
    DragonWindowMotherTabSelected,

    // 아래 둘은 자유 목표(TutorialObjectiveSO) 전용이다. 선형 단계는 "진입 시점 대비"로 재는데
    // 목표는 시작 시점이 없어 절대값·사건으로 봐야 하기 때문이다.
    // 정수로 저장되므로 새 값은 반드시 끝에 추가한다.

    // PopulationManager.AssignedPopulation이 1 이상이다 - 증가분이 아니라 총량으로 본다.
    AnyPopulationAssigned,

    // CycleManager.OnNightEnd - 밤을 넘겼다.
    //
    // 이 조건의 목표는 "낮에 아직 남은 일"로 세지 않는다 - 밤으로 넘어가는 것이 곧 완료 방법이라,
    // 밤 버튼을 누를 때마다 "남은 목표가 있다"고 알리면 절대 지울 수 없는 잔소리가 된다.
    NightSurvived,

    // DragonEggInventorySystem.OnEggGranted - 용의 알을 받았다.
    DragonEggGranted,

    // 지정한 종류의 건물 중 인구가 한 명이라도 들어간 것이 있다.
    // AnyPopulationAssigned는 전체 합계만 보므로 "타워에 배치"를 요구해도 농장에 넣으면 통과해버린다.
    PopulationAssignedToBuilding,

    // DragonTreeManager.ActiveAttributeChanged - 어미용의 속성을 실제로 바꿨다.
    // 창을 열어 본 것(DragonWindowMotherTabSelected)과 달리 바꾸는 행동까지 요구한다.
    MotherDragonAttributeChanged,

    // CycleManager.OnDayStart - 새 날이 시작됐고, 인구가 들어간 생산시설이 있다.
    // 정산 안내는 정산될 것이 있을 때만 뜻이 있다 - 아무것도 안 지은 플레이어에게
    // "자원이 들어왔습니다"라고 하면 화면과 말이 어긋난다.
    DayStartedWithStaffedProduction,

    // BabyDragonGuideController.EggCheckGuideFinished - 알 확인 안내를 읽고 인벤토리를 닫았다.
    // ExclusiveModeClosed로는 대체할 수 없다 - 알을 받기 전에 인벤토리를 한 번 열었다 닫기만 해도
    // 통과해 안내가 시작되기도 전에 목표가 완료됐다.
    BabyDragonEggChecked,

    // 지정한 종류의 건물이 필요한 개수만큼 서 있다(TutorialStepSO의 RequiredCount·RequiresStaffed).
    // "한 기는 손잡고 짓고 나머지는 자율"을 표현하는 조건이라 증가분이 아니라 총량으로 센다.
    //
    // 정원 옵션이 있는 이유: 타워의 공격 속도가 충원율에 비례하므로(TowerAttack.GetAttackInterval)
    // 인구가 1명뿐인 타워 3기는 개수만 채웠을 뿐 화력이 1/5이다. 밤을 넘기는 기준으로 쓰려면
    // "정원을 채운 타워"를 세야 한다.
    BuildingCountReached,

    // 일꾼 모드에서 우클릭했다. 인구 감소가 아니라 "그 조작을 해봤는가"를 본다 -
    // 회수는 흔적이 남지 않는 조작이라 인구 증감으로 재면 뺄 인구가 없을 때 영영 통과하지 못하고,
    // 채운 인구를 일부러 다시 빼는 순서를 강요하게 된다.
    WorkerModeRightClicked,
}
