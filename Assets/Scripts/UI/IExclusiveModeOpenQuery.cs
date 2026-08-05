using UnityEngine;

/// <summary>
/// 배타 모드를 지금 열어도 되는지 묻는다. 안내가 도는 동안 아직 알려주지 않은 창이 열리는 것을 막는 용도다.
/// 배선되지 않은 씬에서는 UIManager가 null로 두고 전부 허용한다(기존 동작 유지).
/// </summary>
public interface IExclusiveModeOpenQuery
{
    bool CanOpen(MonoBehaviour mode);
}
