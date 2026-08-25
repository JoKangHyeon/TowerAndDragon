// 랜드마크 UI 스크립트 2개 이상이 공유하는 로컬 키 (CLAUDE.md 커밋규칙 §3.2).
// 랜드마크별 이름/설명 키는 LandmarkDataSO가 직접 들고 있으므로 여기 포함하지 않는다.
public static class LandmarkLocKeys
{
    public const string STATE_UNCONQUERED = "landmark_state_unconquered";
    public const string STATE_CLAIMED = "landmark_state_claimed";
    public const string STATE_IDLE = "landmark_state_idle";
    public const string STATE_OPERATING = "landmark_state_operating";

    public const string POPULATION_LABEL = "landmark_population_label";

    public static string ResolveStateLocKey(Landmark landmark)
    {
        if (landmark == null || !landmark.IsConquered)
        {
            return STATE_UNCONQUERED;
        }

        // 가동할 수 없는 랜드마크(정원 0 - 타워 유적 등)는 애초에 인구를 받지 못한다.
        // LandmarkPopulation이 붙지 않아 IsOperating이 영원히 false인데, 그걸 IDLE
        // ("점령함 (가동 대기)")로 표시하면 넣을 수 없는 인구를 기다리라고 말하는 셈이 된다.
        if (landmark.Data == null || !landmark.Data.IsOperable)
        {
            return STATE_CLAIMED;
        }

        return landmark.IsOperating ? STATE_OPERATING : STATE_IDLE;
    }
}
