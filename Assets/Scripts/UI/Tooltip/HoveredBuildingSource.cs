using UnityEngine;

/// <summary>
/// 커서 아래에 있는 건물을 찾는 방법.
///
/// 판정 방식은 바뀔 수 있어 이 부분만 따로 떼어 둔다 - 지금은 그리드 셀로 찾지만
/// (<see cref="GridCellHoveredBuildingSource"/>), 배치된 오브젝트를 직접 집는 방식으로 가게 되면
/// 이 클래스를 상속한 구현을 하나 더 만들어 인스펙터에서 갈아끼우면 된다.
/// 툴팁 드라이버와 내용 제공자는 어느 쪽이든 그대로 쓴다.
/// </summary>
public abstract class HoveredBuildingSource : MonoBehaviour
{
    /// <summary>커서 아래 건물을 찾는다. 없으면 false를 돌려주고 building은 null이다.</summary>
    public abstract bool TryGetHoveredBuilding(out Building building);
}
