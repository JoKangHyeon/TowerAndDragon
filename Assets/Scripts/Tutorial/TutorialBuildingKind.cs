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
}
