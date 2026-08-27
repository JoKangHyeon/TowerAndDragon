using System;
using UnityEngine;

// 웨이브 예고 카드의 분류 한 줄 배선(분류 이름 카드 + 아이콘 슬롯이 들어갈 줄).
// 담당 분류를 인스펙터에서 고르게 해, 줄 순서를 바꾸거나 한 분류를 임시로 빼는 것을
// 스크립트 수정 없이 할 수 있게 한다.
//
// 이름 카드와 슬롯 줄이 형제로 놓인 프리팹 구조를 그대로 받는다 - 둘을 한 부모로 묶지 않는 대신
// 카드가 둘을 같이 켜고 끄므로, 빈 분류가 레이아웃에 자리를 남기지 않는다.
[Serializable]
public class WavePreviewCategorySection
{
    [Tooltip("이 줄이 담당하는 표시용 분류.")]
    public MonsterDisplayCategory Category;

    [Tooltip("분류 이름 카드(panel_Type_*). 이 분류의 적이 오늘 없으면 슬롯 줄과 함께 끈다.")]
    public GameObject CategoryCard;

    [Tooltip("아이콘 슬롯이 들어갈 줄(Slot_*). HorizontalLayoutGroup을 붙여 둔다.")]
    public RectTransform SlotContainer;
}
