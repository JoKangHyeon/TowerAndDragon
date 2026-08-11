using UnityEngine;

/// <summary>
/// 건물 종류별 툴팁 내용 제공자. 드라이버는 등록된 제공자에게 차례로 물어보고 처음 응답한 것을 쓴다 -
/// 타워·생산시설 툴팁을 나중에 붙일 때 드라이버를 고치지 않아도 되게 나눠 둔다.
/// </summary>
public abstract class BuildingTooltipProvider : MonoBehaviour
{
    /// <summary>이 건물의 툴팁 내용을 만든다. 담당하지 않는 종류거나 보여줄 것이 없으면 false.</summary>
    public abstract bool TryBuild(Building building, out TooltipContent content);
}
