using System.Collections.Generic;
using UnityEngine;

// 연구 트리 격자 배치 계산. 가로축 = 갈래, 세로축 = 티어.
// Content의 pivot이 (0.5, 0.5)라는 전제로 원점 기준 오프셋을 돌려준다.
// 용 스킬트리의 DragonSkillTreeLayout과 같은 역할(순수 계산, MonoBehaviour 아님).
//
// 갈래 열 폭은 **갈래마다 다르다**. 하나의 고정 폭을 쓰면 한 칸에 들어갈 수 있는 노드 수가
// 그 폭에 갇혀(실측 3개) 기획이 노드를 더 넣지 못한다. 그래서 갈래별로 "가장 붐비는 티어"에
// 맞춰 폭을 정하고, 그 폭들을 왼쪽부터 이어 붙인다.
public static class ResearchTreeLayout
{
    private const float HALF = 0.5f;

    /// <summary>티어 행의 중심 Y. 티어 1이 맨 위에 오도록 아래로 내려간다(시각화 HTML과 동일).</summary>
    public static float RowCenterY(int tierIndex, int tierCount, float rowHeight)
    {
        return ((tierCount - 1) * HALF - tierIndex) * rowHeight;
    }

    /// <summary>같은 갈래·티어 칸에 여러 노드가 있을 때 칸 중심 기준 가로 오프셋.</summary>
    public static float NodeOffsetX(int indexInCell, int countInCell, float nodeSpacing)
    {
        return (indexInCell - (countInCell - 1) * HALF) * nodeSpacing;
    }

    /// <summary>
    /// 갈래별 열 폭을 "가장 붐비는 티어"에서 뽑는다.
    /// 노드 <paramref name="maxNodesInAnyTier"/>개가 <paramref name="nodeSpacing"/> 간격으로
    /// 늘어선 폭에 노드 하나 크기를 더한 값이며, <paramref name="minWidth"/>보다는 넓다.
    /// </summary>
    public static float BranchWidth(int maxNodesInAnyTier, float nodeWidth, float nodeSpacing, float minWidth)
    {
        int count = Mathf.Max(1, maxNodesInAnyTier);
        float needed = (count - 1) * nodeSpacing + nodeWidth;
        return Mathf.Max(minWidth, needed);
    }

    /// <summary>
    /// 갈래 폭 목록을 받아 각 갈래의 중심 X를 채운다. 전체가 원점 기준 좌우 대칭이 되도록 놓는다.
    /// </summary>
    public static void ResolveBranchCenters(
        IReadOnlyList<float> branchWidths, float branchGap, IList<float> centersOut)
    {
        centersOut.Clear();

        float total = 0f;
        for (int i = 0; i < branchWidths.Count; i++)
        {
            total += branchWidths[i];
        }

        total += branchGap * Mathf.Max(0, branchWidths.Count - 1);

        float cursor = -total * HALF;
        for (int i = 0; i < branchWidths.Count; i++)
        {
            centersOut.Add(cursor + branchWidths[i] * HALF);
            cursor += branchWidths[i] + branchGap;
        }
    }

    public static Vector2 NodePosition(
        float branchCenterX,
        int tierIndex,
        int tierCount,
        float rowHeight,
        int indexInCell,
        int countInCell,
        float nodeSpacing)
    {
        return new Vector2(
            branchCenterX + NodeOffsetX(indexInCell, countInCell, nodeSpacing),
            RowCenterY(tierIndex, tierCount, rowHeight));
    }
}
