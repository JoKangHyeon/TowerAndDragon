using UnityEngine;

// 연구 트리 격자 배치 계산. 가로축 = 갈래(3열), 세로축 = 티어(5행).
// Content의 pivot이 (0.5, 0.5)라는 전제로 원점 기준 오프셋을 돌려준다.
// 용 스킬트리의 DragonSkillTreeLayout과 같은 역할(순수 계산, MonoBehaviour 아님).
public static class ResearchTreeLayout
{
    private const float HALF = 0.5f;

    /// <summary>갈래 열의 중심 X. 열들이 원점을 기준으로 좌우 대칭이 되도록 배치한다.</summary>
    public static float ColumnCenterX(int branchIndex, int branchCount, float columnWidth)
    {
        return (branchIndex - (branchCount - 1) * HALF) * columnWidth;
    }

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

    public static Vector2 NodePosition(
        int branchIndex,
        int branchCount,
        float columnWidth,
        int tierIndex,
        int tierCount,
        float rowHeight,
        int indexInCell,
        int countInCell,
        float nodeSpacing)
    {
        float x =
            ColumnCenterX(branchIndex, branchCount, columnWidth) +
            NodeOffsetX(indexInCell, countInCell, nodeSpacing);

        return new Vector2(x, RowCenterY(tierIndex, tierCount, rowHeight));
    }
}
