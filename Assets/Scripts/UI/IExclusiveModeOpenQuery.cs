using UnityEngine;

/// <summary>
/// 배타 모드를 지금 열어도 되는지 묻는다. 안내가 도는 동안 아직 알려주지 않은 창이 열리는 것을 막는 용도다.
/// 배선되지 않은 씬에서는 UIManager가 null로 두고 전부 허용한다(기존 동작 유지).
/// </summary>
public interface IExclusiveModeOpenQuery
{
    bool CanOpen(MonoBehaviour mode);

    /// <summary>
    /// 단축키로 이 창을 닫아도 되는지. 안내가 그 창 안을 가리키는 동안 닫아버리면
    /// 가리킬 대상이 사라져 무엇을 하라는 것인지 알 수 없게 된다 - 닫으라고 할 때까지 막는다.
    /// 창의 자체 닫기 버튼·ESC까지는 막지 않는다(그쪽은 딤이 이미 가린다).
    /// </summary>
    bool CanClose(MonoBehaviour mode);
}
