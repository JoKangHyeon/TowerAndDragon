using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 자원 표시 1칸: 자원 종류 ↔ 수량 텍스트(+ 선택적으로 아이콘).
// UI_IngameWindow(HUD 상단)와 UI_ResourceAmountPanel(용 창 슬라임 현황)이 같은 배열 타입을
// 인스펙터에 노출해야 하므로 최상위 타입으로 뺐다.
//
// 필드 이름(Type/AmountText/IconImage)은 바꾸지 말 것 - Unity는 배열 원소를 필드 이름으로
// 역직렬화하므로, 이름이 유지되는 동안에만 기존 프리팹 배선이 살아남는다.
[Serializable]
public struct ResourceAmountSlot
{
    public ResourceType Type;

    public TMP_Text AmountText;

    [Tooltip("비워두면 프리팹에 배치된 아이콘을 그대로 쓴다. 지정하면 ResourceData의 아이콘으로 덮어쓴다.")]
    [WiringOptional]
    public Image IconImage;
}
