using System.Collections.Generic;
using System.Text;

/// <summary>
/// 활성 뮤테이터 목록을 툴팁 문구로 만든다. 툴팁 프레임워크(TooltipContent·UI_TooltipPresenter)는
/// 도메인을 모르므로, 새 게임 + 전용 표현은 여기에만 둔다
/// (ResourceForecastTooltipBuilder와 같은 계층·같은 관용구다).
///
/// 출력 예)
///   새 게임 + 제약
///   흉년 2단계
///   대식가 1단계
///   철인 모드
/// </summary>
public static class RunMutatorTooltipBuilder
{
    // 제목. 이 툴팁이 무엇의 목록인지 알려 준다.
    private const string TITLE_LOC_KEY = "hud_newgameplus_tooltip_title";

    // 단계가 있는 뮤테이터 한 줄. {0} = 이름, {1} = 단계 번호.
    private const string TIER_ROW_LOC_KEY = "mutator_tier_row";

    // 단계가 하나뿐인(규칙 계열) 뮤테이터 한 줄. {0} = 이름.
    // "철인 모드 1단계"는 단계가 있는 것처럼 읽혀서 오히려 정보를 흐린다.
    private const string SINGLE_TIER_ROW_LOC_KEY = "mutator_single_tier_row";

    // 툴팁 본문은 한 줄에 한 뮤테이터씩 쌓는다. 런 시작 시 한 번 만들고 언어가 바뀔 때만
    // 다시 만들므로 StringBuilder로 충분하다.
    private static readonly StringBuilder BODY_BUILDER = new();

    /// <summary>활성 뮤테이터를 단계까지 나열한 툴팁 내용. 목록이 비면 내용 없는 값을 돌려준다.</summary>
    public static TooltipContent Build(IReadOnlyList<RunMutatorSelection> selections)
    {
        if (selections == null || selections.Count == 0)
        {
            return default;
        }

        BODY_BUILDER.Clear();

        for (int i = 0; i < selections.Count; i++)
        {
            RunMutatorSelection selection = selections[i];

            if (selection.Mutator == null)
            {
                continue;
            }

            AppendRow(selection);
        }

        return new TooltipContent(StringTable.GetString(TITLE_LOC_KEY), BODY_BUILDER.ToString());
    }

    private static void AppendRow(RunMutatorSelection selection)
    {
        if (BODY_BUILDER.Length > 0)
        {
            BODY_BUILDER.AppendLine();
        }

        string name = StringTable.GetString(selection.Mutator.NameLocKey);

        // 단계 수가 1이면 켜짐/꺼짐 뮤테이터다 - 단계 번호를 붙이지 않는다.
        if (selection.Mutator.TierCount <= RunMutatorSO.FIRST_TIER)
        {
            BODY_BUILDER.AppendFormat(StringTable.GetString(SINGLE_TIER_ROW_LOC_KEY), name);
            return;
        }

        BODY_BUILDER.AppendFormat(
            StringTable.GetString(TIER_ROW_LOC_KEY), name, selection.Tier);
    }
}
