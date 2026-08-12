using UnityEngine;

/// <summary>
/// 단축키를 지금 받아도 되는지 묻는다. 키보드 입력은 안내 오버레이의 딤을 통과하므로,
/// 막지 않으면 <b>마우스로는 못 하는 조작이 키로는 된다</b> - 팁 체인이 도는 동안 딤이 밤 버튼을
/// 덮고 있는데도 단축키로 건설·점령·워커 창을 여닫을 수 있었던 것이 그 예다.
///
/// 기본 차단은 <see cref="UI_GuideOverlay.IsBlockingInput"/> 하나로 정한다(마우스를 막는 것과 같은 기준).
/// 이 인터페이스는 그것만으로 부족한 두 경우를 맡는다.
/// - <see cref="BlocksShortcuts"/> : 딤이 없는 단계에서도 계속 막아야 하는 안내
/// - <see cref="AllowsShortcut"/> : 막힌 상태에서도 지금 단계가 시킨 키 하나는 통과시켜야 하는 경우
///
/// 등록되지 않은 씬에서는 목록이 비어 딤 여부만으로 판정된다(다른 관문과 같은 관례).
/// </summary>
public interface IShortcutBlockQuery
{
    /// <summary>딤과 무관하게 안내가 도는 내내 단축키를 막을지.</summary>
    bool BlocksShortcuts();

    /// <summary>
    /// 막힌 상태에서도 이 단축키만은 허용할지. 지금 단계가 누르라고 시킨 조작에만 쓴다.
    /// mode가 null이면 배타 모드와 무관한 단축키다(일시정지 등) - 그런 키를 단계가 시키는 일은 없다.
    /// </summary>
    bool AllowsShortcut(MonoBehaviour mode);
}
