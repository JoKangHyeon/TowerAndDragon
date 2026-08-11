/// <summary>
/// 새끼용 가이드(획득 → 인벤토리 → 부화 → 배치)의 진행 단계.
/// 선언 순서가 곧 진행 순서다 - BabyDragonGuideController가 단조 전진을 판정할 때 값을 비교한다.
/// </summary>
public enum BabyDragonGuideStep
{
    OpenInventory,
    WaitHatch,
    PlaceDragon,
    Completed,
}
