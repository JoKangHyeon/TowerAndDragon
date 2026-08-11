/// <summary>
/// 런타임에 생성돼 GuideAnchor를 붙일 수 없는 대상. 창이 직접 RectTransform을 돌려준다.
///
/// 원래는 "새끼용 인벤토리 슬롯" bool 하나였다. 그때는 알 탭과 용 탭이 따로라 "지금 열린 탭의 첫 슬롯"으로
/// 충분했지만, 두 목록이 한 패널에 함께 놓이면서 어느 쪽을 가리킬지 단계가 직접 정해야 한다.
///
/// 값을 중간에 끼워 넣지 말 것 - 스텝 에셋에 정수로 저장되므로 기존 단계가 다른 대상을 가리키게 된다.
/// </summary>
public enum TutorialDynamicTargetKind
{
    None = 0,

    /// <summary>용 창 새끼용 탭의 첫 알 슬롯.</summary>
    DragonEggSlot,

    /// <summary>용 창 새끼용 탭의 첫 새끼용 슬롯.</summary>
    BabyDragonSlot,

    /// <summary>첫 새끼용 슬롯의 위치 표시/신발 버튼.</summary>
    BabyDragonFocusButton,
}
