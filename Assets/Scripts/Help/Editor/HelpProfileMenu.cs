using UnityEditor;
using UnityEngine;

/// <summary>
/// 도감 이력을 지우는 메뉴. "최초 조우"는 한 번뿐이라 이것 없이는 검증을 반복할 수 없다.
/// 에디터 전용 도구라 CLAUDE.md 5번 예외(문자열·리터럴 상수 규칙 미적용)에 해당한다.
/// </summary>
public static class HelpProfileMenu
{
    private const string MENU_RESET_PROFILE = "Tools/Help/도감 이력 초기화 (해금·열람)";
    private const string MENU_RESET_VIEWED = "Tools/Help/도감 열람 이력만 초기화";

    [MenuItem(MENU_RESET_PROFILE)]
    public static void ResetProfile()
    {
        HelpProfile.ResetAll();
        Debug.Log("[HelpProfile] 해금·열람 이력을 초기화했습니다.");
    }

    // 해금을 남기고 붉은 점만 되살린다. 항목 대부분이 AutoPopup이라, 위 메뉴로 전부 지우면
    // 붉은 점을 다시 보려면 한 판을 다시 해서 최초 조우 트리거를 전부 다시 밟아야 한다.
    [MenuItem(MENU_RESET_VIEWED)]
    public static void ResetViewedOnly()
    {
        HelpProfile.ResetViewedOnly();
        Debug.Log("[HelpProfile] 열람 이력을 초기화했습니다(해금은 유지).");
    }
}
