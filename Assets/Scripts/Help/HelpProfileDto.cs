using System.Collections.Generic;

/// <summary>
/// 플레이어 프로필에 저장되는 도감 해금 이력. 세이브 슬롯이 아니라 플레이어 단위로 남는다.
///
/// 지금은 문자열 목록 하나뿐이라 마이그레이션이 없지만 <see cref="CURRENT_VERSION"/>을 미리 둔다 -
/// 나중에 "NEW 배지"·"열람 이력"을 더할 때 구버전 판정 수단이 없으면 만들 방법이 사라진다.
/// SaveSchema.CURRENT_VERSION과 공유하지 않는다: 세이브 포맷이 바뀔 때마다 도감이 무효화되면
/// "다른 세이브에도 이어진다"는 이 기능의 취지가 무너진다.
/// </summary>
public sealed class HelpProfileDto
{
    public const int CURRENT_VERSION = 1;

    public int SchemaVersion = CURRENT_VERSION;

    // 카탈로그에서 빠진 id도 그대로 보존한다 - 항목을 뺐다가 다시 넣었을 때
    // 이미 본 팝업이 되살아나면 안 된다.
    public List<string> UnlockedEntryIds = new();
}
