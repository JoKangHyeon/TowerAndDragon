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
}
