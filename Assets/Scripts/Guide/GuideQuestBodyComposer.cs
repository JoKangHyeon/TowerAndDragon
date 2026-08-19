using UnityEngine;

/// <summary>
/// 퀘스트 설명을 <b>표시하는 그 순간</b>의 값으로 완성한다.
///
/// 설명문에 숫자를 박아 두면 쓴 시점에만 참이다. "인구 40명이니 하루 40이 나가고 재고 250으로는 엿새"는
/// 1일차 시작에서만 맞고, 하루만 지나도 화면과 말이 갈린다.
/// 그래서 변하는 숫자는 문안에서 빼고 여기서 붙인다 - 언제 열어 보든 지금 상황을 말한다.
///
/// 붙이는 문장은 상황에 따라 아예 다른 키를 쓴다. 흑자인데 "며칠이면 바닥납니다"라고 할 수는 없고,
/// 하나의 문장에 조건을 욱여넣으면 번역이 불가능해진다.
/// </summary>
public sealed class GuideQuestBodyComposer : MonoBehaviour
{
    // "지금 식량 {0}에 하루 {1}씩 줄고 있습니다. 이대로면 {2}일 뒤 바닥납니다."
    private const string FOOD_SHORTAGE_LOC_KEY = "guide_outlook_food_shortage";

    // "지금 식량 {0}에 하루 {1}씩 늘고 있습니다."
    private const string FOOD_SURPLUS_LOC_KEY = "guide_outlook_food_surplus";

    // 하루 증감이 정확히 0일 때. 늘지도 줄지도 않으므로 남은 날을 셀 수 없다.
    private const string FOOD_BREAK_EVEN_LOC_KEY = "guide_outlook_food_break_even";

    // TMP 인라인 스프라이트 마크업. 표시할 문구가 아니라 서식이므로 스트링테이블로 빼지 않는다.
    private const string SPRITE_TAG_FORMAT = "<sprite name=\"{0}\">";

    [Tooltip("지금 보유량을 묻는다.")]
    [WiringOptional]
    [SerializeField] private ResourceManager _resourceManager;

    [Tooltip("하루 예상 증감을 묻는다. 자원 UI와 같은 출처여야 표기와 설명이 어긋나지 않는다.")]
    [WiringOptional]
    [SerializeField] private ResourceForecast _resourceForecast;

    /// <summary>
    /// 설명 문안에 넣을 서식 인자. 문구 자체는 여전히 스트링테이블 키로 넘기고 숫자만 여기서 만든다 -
    /// 그래야 표시하는 쪽(카드·말풍선)이 언어가 바뀔 때 문안을 다시 해석할 수 있다.
    /// 덧붙일 것이 없으면 빈 배열이다(그런 퀘스트의 문안에는 자리표시자가 없다).
    ///
    /// 다만 <b>인자로 넣는 문장 자체는 만들 때의 언어로 굳는다</b> - 카드를 열어 둔 채 언어를 바꾸면
    /// 그 문장만 옛 언어로 남는다. 카드를 닫았다 다시 열면 맞춰지므로 그대로 둔다.
    /// </summary>
    public object[] ResolveArguments(GuideQuestSO quest)
    {
        if (quest == null)
        {
            return System.Array.Empty<object>();
        }

        // 자리는 항상 같은 순서로 채운다 - {0} 지금 상황, {1} 인라인 아이콘.
        // 필요 없는 퀘스트는 그 자리를 문안에서 쓰지 않으면 그만이고, 빈 문자열을 넣어 두므로
        // 자리표시자만 남고 인자가 없어 서식이 예외로 터지는 일이 없다.
        return new object[]
        {
            ResolveOutlook(quest.BodyArgument),
            ResolveIcon(quest.BodyIconName),
        };
    }

    // TMP 인라인 스프라이트 태그. 이름은 BuildingIcons 스프라이트 에셋(과 그 fallback)에서 찾는다.
    private static string ResolveIcon(string iconName)
    {
        return string.IsNullOrWhiteSpace(iconName)
            ? string.Empty
            : string.Format(SPRITE_TAG_FORMAT, iconName);
    }

    private string ResolveOutlook(GuideQuestBodyArgument argument)
    {
        if (argument != GuideQuestBodyArgument.FoodOutlook)
        {
            return string.Empty;
        }

        // 둘 중 하나라도 없으면 숫자를 지어내지 않고 조용히 문안만 보여준다.
        if (_resourceManager == null || _resourceForecast == null)
        {
            return string.Empty;
        }

        int stock = _resourceManager.GetAmount(ResourceType.Food);
        int netChange = _resourceForecast.GetDailyNetChange(ResourceType.Food);

        if (netChange > 0)
        {
            return string.Format(StringTable.GetString(FOOD_SURPLUS_LOC_KEY), stock, netChange);
        }

        if (netChange == 0)
        {
            return string.Format(StringTable.GetString(FOOD_BREAK_EVEN_LOC_KEY), stock);
        }

        // 며칠을 더 버티는지. 정산은 하루 단위이므로 남은 날도 내림으로 센다 -
        // 2.9일이면 이틀 뒤 아침까지는 버티고 사흘째 아침에 모자란다.
        int dailyLoss = -netChange;
        int daysLeft = stock / dailyLoss;

        return string.Format(StringTable.GetString(FOOD_SHORTAGE_LOC_KEY), stock, dailyLoss, daysLeft);
    }
}
