/// <summary>
/// BuildingConstructed 조건이 기다릴 건물 종류. 클래스로 구분되는 만큼만 나눈다 -
/// 벌목장·채석장·농장은 모두 Factory 한 클래스이고 ResourceProductionData로만 갈리므로,
/// 그 구분이 필요하면 단계에서 데이터를 함께 지정한다.
/// </summary>
public enum TutorialBuildingKind
{
    None = 0,

    // 새끼용 타워는 제외한다 - 새끼용을 놓은 것으로 "타워를 지었다"가 되면 안 된다.
    AnyTower,

    BabyDragonTower,
    ResearchLab,

    // 종류를 더 좁히려면 단계의 TargetFactoryData를 함께 지정한다.
    Factory,

    // 메인 성. 그리드에 3x3으로 등록돼 있어 클릭하면 다른 건물과 같은 선택 경로를 지난다
    // (BuildingPlacementController.SelectExistingBuildingAt) - 어미용 창을 여는 단계에 쓴다.
    Castle,

    // 슬라임 농장. Factory를 상속하므로 위 Factory로도 통과한다 - 둘을 구분해야 하는 쪽(슬라임 농장만
    // 세는 목표)이 이 값을 쓴다. Factory 쪽을 좁히지 않는 이유는 기존 단계 에셋의 판정을 바꾸지 않기 위해서다.
    // 정수로 저장되므로 새 값은 반드시 끝에 추가한다 - 중간에 끼우면 기존 에셋이 다른 종류를 가리킨다.
    SlimeFactory,

    // 봉인석. 포탈 자리에 세우고 4개가 모두 서면 즉시 승리한다.
    // 정수로 저장되므로 새 값은 반드시 끝에 추가한다.
    SealStone,

    // 종류를 가리지 않는다. 철거처럼 "무엇이든"이 곧 문안인 조건에만 쓴다 -
    // 건설 조건에 쓰면 게임 시작 시 성 등록(GridMap의 사전 배치 경로)으로 즉시 오발화한다.
    // 정수로 저장되므로 새 값은 반드시 끝에 추가한다.
    AnyBuilding,
}
