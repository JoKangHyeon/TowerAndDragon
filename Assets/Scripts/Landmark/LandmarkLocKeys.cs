// 랜드마크 UI 스크립트 2개 이상이 공유하는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 랜드마크별 이름/설명 키는 LandmarkDataSO가 직접 들고 있으므로 여기 포함하지 않는다.
public static class LandmarkLocKeys
{
    public const string STATE_UNCONQUERED = "landmark_state_unconquered";
    public const string STATE_IDLE = "landmark_state_idle";
    public const string STATE_OPERATING = "landmark_state_operating";

    public const string POPULATION_LABEL = "landmark_population_label";

    public static string ResolveStateLocKey(Landmark landmark)
    {
        if (landmark == null || !landmark.IsConquered)
        {
            return STATE_UNCONQUERED;
        }

        return landmark.IsOperating ? STATE_OPERATING : STATE_IDLE;
    }
}
