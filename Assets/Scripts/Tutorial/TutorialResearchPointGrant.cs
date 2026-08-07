using UnityEngine;

/// <summary>
/// 연구 챕터가 시작될 때 연구 점수가 모자라면 채워 준다.
///
/// 연구소 정원은 2명이고 인구 1명당 하루 5점이라, 안내를 따라 1명만 배치하면 다음 날 5점뿐이다.
/// 1티어 노드가 10점을 요구하므로 그대로면 "하나를 골라 연구하세요" 단계에서 아무것도 못 하고 갇힌다.
/// 인구를 정원까지 채우게 시키는 방법도 있지만, 그때 남은 인구가 모자라면 같은 자리에서 또 갇힌다 -
/// 배치 결과와 무관하게 성립하도록 부족분만 메운다.
/// </summary>
public sealed class TutorialResearchPointGrant : MonoBehaviour
{
    private const string RESEARCH_POINTS_GRANTED_LOC_KEY = "tutorial_day2_research_points_granted";

    [SerializeField] private ResearchManager _researchManager;

    [SerializeField] private UI_NotificationToast _toast;

    [Tooltip("이 챕터를 시작할 때 최소한 갖고 있어야 할 연구 점수. 1티어 노드 비용 이상으로 둔다.")]
    [Min(0)]
    [SerializeField] private int _minimumResearchPoints;

    private void OnEnable()
    {
        if (_researchManager == null)
        {
            Debug.LogError("[TutorialResearchPointGrant] ResearchManager 참조가 없습니다.", this);
            return;
        }

        int missing = _minimumResearchPoints - _researchManager.ResearchPoints;
        if (missing <= 0)
        {
            return;
        }

        _researchManager.AddResearchPoints(missing);

        // 이 컴포넌트는 Tutorial System 프리팹 안에 있고 토스트는 씬 UI라 프리팹 기본 참조로
        // 연결할 수 없다. 씬 오버라이드가 없거나 풀렸을 때만 한 번 찾아 안내가 사라지지 않게 한다.
        UI_NotificationToast toast = _toast != null
            ? _toast
            : FindFirstObjectByType<UI_NotificationToast>();
        toast?.Show(RESEARCH_POINTS_GRANTED_LOC_KEY, missing);
    }
}
