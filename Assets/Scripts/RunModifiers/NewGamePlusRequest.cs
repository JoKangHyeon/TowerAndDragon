using System;
using System.Collections.Generic;

/// <summary>"다음 씬 로드에서 이 뮤테이터 조합으로 새 런을 시작하라"는 요청을
/// 씬 경계 너머로 전달하는 우편함. StartScene의 진입 UI가 채우고 SampleScene의
/// <see cref="RunModifierService"/>가 Awake에서 소비한다.
///
/// MonoBehaviour가 아닌 static인 이유(<see cref="SaveLoadRequest"/>와 같다):
///  - 씬 리로드를 넘어 살아남아야 하는데, 값 하나를 넘기는 데 DontDestroyOnLoad 오브젝트를
///    만드는 건 과하고 이 프로젝트에 선례가 없다.
///  - 도메인 리로드 시 초기화되어 안전한 기본값("뮤테이터 없음")으로 돌아간다.
///  - PlayerPrefs는 "하드모드"가 디스크에 눌어붙어 다음 실행이 이상해지는 사고가 있다.
///  - ScriptableObject 런타임 값은 에디터에서의 변경이 에셋에 남아 팀원 간 diff를 만든다.
///
/// 소비 즉시 비워지므로 상태가 남지 않는다 - "싱글톤 금지" 관례가 막으려는 전역 서비스와는
/// 성격이 다르다.</summary>
public static class NewGamePlusRequest
{
    private static (string Id, int Tier)[] _pendingSelections;

    public static bool HasPending { get; private set; }

    public static void Request(IReadOnlyList<(string Id, int Tier)> selections)
    {
        if (selections == null || selections.Count == 0)
        {
            Clear();
            return;
        }

        (string Id, int Tier)[] copied = new (string, int)[selections.Count];

        for (int i = 0; i < selections.Count; i++)
        {
            copied[i] = selections[i];
        }

        _pendingSelections = copied;
        HasPending = true;
    }

    public static bool TryConsume(out (string Id, int Tier)[] selections)
    {
        selections = _pendingSelections ?? Array.Empty<(string, int)>();
        bool hadRequest = HasPending;

        Clear();
        return hadRequest;
    }

    public static void Clear()
    {
        HasPending = false;
        _pendingSelections = null;
    }
}
