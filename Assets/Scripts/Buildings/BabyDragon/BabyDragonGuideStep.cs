/// <summary>
/// 새끼용 가이드(획득 → 인벤토리 → 부화 → 배치 → 모드 전환)의 진행 단계.
/// 선언 순서가 곧 진행 순서다 - BabyDragonGuideController가 단조 전진을 판정할 때 값을 비교한다.
/// 새 단계는 반드시 뒤에 붙인다 - 중간에 끼우면 RunData.EnteredGuideSteps에 저장된 값이 다른 단계를 가리킨다.
/// </summary>
public enum BabyDragonGuideStep
{
    OpenInventory,
    WaitHatch,
    PlaceDragon,
    Completed,

    // 배치한 새끼용을 클릭해 관리창을 열면 공격/버프 모드 버튼을 가리켜 역할을 바꿀 수 있음을 알린다.
    ChangeMode,
}
