/// <summary>
/// "다음 씬 로드에서 이 슬롯을 이어하기 하라"는 요청을 씬 경계 너머로 전달하는 우편함.
///
/// MonoBehaviour가 아닌 static인 이유:
///  - 씬 리로드를 넘어 살아남아야 하는데, 값 하나(슬롯 인덱스)를 넘기는 데 DontDestroyOnLoad
///    오브젝트를 만드는 건 과하고 이 프로젝트에 선례가 없다.
///  - 도메인 리로드 시 초기화되어 안전한 기본값("새 게임")으로 돌아간다.
///  - PlayerPrefs는 "이어하기 모드"가 디스크에 눌어붙어 다음 실행이 이상해지는 사고가 있다.
///  - ScriptableObject 런타임 값은 에디터에서의 변경이 에셋에 남아 팀원 간 diff를 만든다.
///
/// 소비 즉시 비워지므로 상태가 남지 않는다 - "싱글톤 금지" 관례가 막으려는 전역 서비스와는 성격이 다르다.
/// </summary>
public static class SaveLoadRequest
{
    public static bool HasPending { get; private set; }

    public static int PendingSlotIndex { get; private set; } = SavePaths.INVALID_SLOT_INDEX;

    public static void Request(int slotIndex)
    {
        if (!SavePaths.IsValidSlotIndex(slotIndex))
        {
            UnityEngine.Debug.LogError($"[SaveLoadRequest] 잘못된 슬롯 인덱스: {slotIndex}");
            return;
        }

        HasPending = true;
        PendingSlotIndex = slotIndex;
    }

    public static bool TryConsume(out int slotIndex)
    {
        slotIndex = PendingSlotIndex;
        bool hadRequest = HasPending;

        HasPending = false;
        PendingSlotIndex = SavePaths.INVALID_SLOT_INDEX;
        return hadRequest;
    }

    public static void Clear()
    {
        HasPending = false;
        PendingSlotIndex = SavePaths.INVALID_SLOT_INDEX;
    }
}
