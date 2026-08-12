/// <summary>에셋·프리팹 이름에서 스트링테이블 키를 만든다.
/// (예: "TD_CrossBow" + 접두사 "buildMode_panel_tower_info_" → "buildMode_panel_tower_info_crossBow")
///
/// 이름을 바꾸면 키가 어긋나지만, 그때는 StringTable이 키 문자열을 그대로 돌려주므로
/// 화면에 "buildMode_panel_..."이 그대로 보여 바로 드러난다.
/// 빌드모드의 타워·생산건물 정보 팝업이 같은 규칙을 쓰므로 여기 하나만 둔다(CLAUDE.md 커밋규칙 §3.2).</summary>
public static class LocKeySuffix
{
    /// <summary>namePrefixes 중 하나로 시작하면 그 접두사를 떼고, 첫 글자를 소문자로 바꿔 keyPrefix에 붙인다.</summary>
    public static string Build(string keyPrefix, string assetName, params string[] namePrefixes)
    {
        if (string.IsNullOrEmpty(assetName))
        {
            return string.Empty;
        }

        string suffix = assetName;

        foreach (string namePrefix in namePrefixes)
        {
            if (!string.IsNullOrEmpty(namePrefix) && suffix.StartsWith(namePrefix))
            {
                suffix = suffix.Substring(namePrefix.Length);
                break;
            }
        }

        if (suffix.Length == 0)
        {
            return string.Empty;
        }

        return keyPrefix + char.ToLowerInvariant(suffix[0]) + suffix.Substring(1);
    }
}
