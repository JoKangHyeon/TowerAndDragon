using System.Collections.Generic;

/// <summary>
/// 플레이어 프로필에 저장되는 도감 해금 이력. 세이브 슬롯이 아니라 플레이어 단위로 남는다.
///
/// <see cref="CURRENT_VERSION"/>을 미리 둔 덕에 v2에서 "열람 이력"을 더할 수 있었다 - 올리지
/// 않으면 앞으로 빈 <see cref="ViewedEntryIds"/>가 "아직 아무것도 안 봤다"인지 "구버전이라 기록이
/// 없다"인지 구분할 수단이 사라진다.
/// SaveSchema.CURRENT_VERSION과 공유하지 않는다: 세이브 포맷이 바뀔 때마다 도감이 무효화되면
/// "다른 세이브에도 이어진다"는 이 기능의 취지가 무너진다.
/// </summary>
public sealed class HelpProfileDto
{
    public const int CURRENT_VERSION = 2;

    public int SchemaVersion = CURRENT_VERSION;

    // 카탈로그에서 빠진 id도 그대로 보존한다 - 항목을 뺐다가 다시 넣었을 때
    // 이미 본 팝업이 되살아나면 안 된다.
    public List<string> UnlockedEntryIds = new();

    // v2에서 추가. "도감 창에서 본문을 실제로 펼쳐 봤는가"만 뜻한다 - 해금("만났다")과 소비자가
    // 달라(붉은 점) 한 리스트로 합치지 않는다. 최초 조우 팝업은 여기에 넣지 않는다.
    // UnlockedEntryIds와 같은 이유로 카탈로그에서 빠진 id도 보존한다.
    public List<string> ViewedEntryIds = new();
}
