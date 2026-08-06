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
    [SerializeField] private ResearchManager _researchManager;

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
        _researchManager.AddResearchPoints(missing);
    }
}
