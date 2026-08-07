/// <summary>
/// UI_GuideOverlay 표시권 우선순위. 숫자가 클수록 화면을 먼저 차지한다.
/// 오버레이는 이 값들을 모르고 크기만 비교하므로, 어떤 안내가 어떤 안내를 이기는지는 여기 한 곳에서만 정해진다.
/// </summary>
public static class GuidePriority
{
    // 두 가이드의 첫 단계가 같은 프레임에 둘 다 화면을 덮으려 한다(양쪽 다 blocksInput).
    // 1일차 튜토리얼이 이기고, 새끼용 가이드는 단계를 계속 전진시키되 그리지 않는다.
    public const int BABY_DRAGON_GUIDE = 100;
    public const int DAY_ONE_TUTORIAL = 200;
}
