using UnityEditor;
using UnityEngine;

/// <summary>
/// 도감 해금 이력을 지우는 메뉴. "최초 조우"는 한 번뿐이라 이것 없이는 검증을 반복할 수 없다.
/// 에디터 전용 도구라 CLAUDE.md 5번 예외(문자열·리터럴 상수 규칙 미적용)에 해당한다.
/// </summary>
public static class HelpProfileMenu
{
    private const string MENU_RESET_PROFILE = "Tools/Help/도감 해금 이력 초기화";

    [MenuItem(MENU_RESET_PROFILE)]
    public static void ResetProfile()
    {
        HelpProfile.ResetAll();
        Debug.Log("[HelpProfile] 해금 이력을 초기화했습니다.");
    }
}
