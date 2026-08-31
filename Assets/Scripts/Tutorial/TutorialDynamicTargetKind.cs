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

    /// <summary>
    /// 인구 패널에서 타워의 공격속도·초당피해 두 행을 함께 덮는 영역.
    ///
    /// 앵커로 잡을 수 없는 이유: 산출 행은 건물 종류마다 개수도 뜻도 달라 런타임에 풀에서 만들어지고,
    /// 체력·공격력 행이 빠지는 타워도 있어 <b>행 번호가 고정되지 않는다</b>.
    /// <see cref="GuideAnchorId.PopulationOutputRow"/>는 정의상 첫 산출 행(타워면 가동률)이라
    /// "인구를 채우면 공격이 빨라진다"를 가리키기에 맞지 않는다.
    /// </summary>
    TowerAttackSpeedRows,
}
