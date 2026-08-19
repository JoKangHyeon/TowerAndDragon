using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 안내가 시키는 일을 할 수 있을 만큼은 갖고 있게 보장한다. 챕터 오브젝트에 붙여, 그 챕터가 열릴 때
/// 하한선에 못 미치는 만큼만 채운다.
///
/// 행동형 단계는 그 행동이 실제로 가능해야 성립하는데, 자원은 그때까지 플레이어가 무엇을 했느냐에
/// 통째로 달려 있다. 타워를 계속 지어 자원을 비워 두면 안내가 "지으세요"·"파병하세요"라고 해도
/// 할 수 없고, 그 단계는 스톨 탈출로 미완료인 채 넘어간다. 그 목표가 밤 관문에 걸려 있으면
/// (TutorialObjectiveController.CanEndDay) 밤이 영영 오지 않아 되돌릴 방법이 없어진다.
///
/// <b>인구가 자원보다 먼저 막힌다.</b> 파병이 요구하는 인구는 총인구가 아니라 <b>놀고 있는 인구</b>인데
/// (UI_ConquestWindow가 PopulationManager.AvailablePopulation을 넘긴다), 정작 앞 안내가
/// "정원을 채우세요"라고 시킨다. 시킨 대로 다 채운 플레이어는 자원이 아무리 많아도 파병하지 못한다.
///
/// 그래서 모자란 쪽만 메운다 - 넉넉하면 아무것도 하지 않으므로 정상적으로 모아 온 플레이어의
/// 살림에는 흔적이 남지 않는다. 지급이 아니라 <b>하한선</b>이다.
/// (TutorialResearchPointGrant가 연구 점수에 같은 방식을 쓴다.)
///
/// 비용을 0으로 만드는 방법을 쓰지 않는 이유가 둘 있다. 점령 비용 감면(IConquestModifierQuery)은
/// 인구를 감면 대상에서 빼므로(ConquestManager.ApplyCostReduction) 정작 막히는 것이 안 풀리고,
/// 그 자리는 연구 감면이 이미 쓰고 있어 서로 덮어쓴다. 무엇보다 창에 뜬 비용과 안내의 설명이 어긋난다.
/// </summary>
public sealed class TutorialAffordanceGrant : MonoBehaviour
{
    // "안내를 이어갈 수 있도록 부족한 자원을 채웠습니다."
    private const string RESOURCE_GRANTED_LOC_KEY = "tutorial_grant_resources";
    // "안내를 이어갈 수 있도록 인구를 늘렸습니다."
    private const string POPULATION_GRANTED_LOC_KEY = "tutorial_grant_population";

    [Tooltip("자원 부족분을 채운다.")]
    [SerializeField] private ResourceManager _resourceManager;

    [Tooltip("놀고 있는 인구가 모자라면 최대 인구를 올려 만든다.")]
    [SerializeField] private PopulationManager _populationManager;

    [Tooltip("이 챕터를 넘기는 데 필요한 놀고 있는 인구. 필요 없으면 0으로 둔다.")]
    [Min(0)]
    [SerializeField] private int _requiredIdlePopulation;

    [Tooltip("이 챕터를 넘기는 데 필요한 자원의 하한선. 시키는 것을 다 해도 바닥나지 않게 여유를 얹어 둔다.")]
    [SerializeField] private List<ResourceAmount> _requiredResources = new();

    [Tooltip("채워 줬다는 사실을 알린다. 비우면 조용히 채운다.")]
    [SerializeField] private UI_NotificationToast _toast;

    // 챕터 오브젝트가 껐다 켜져도 두 번 채우지 않는다.
    private bool _hasGranted;

    // 챕터가 열릴 때 한 번 돈다 - 안내가 도는 동안에는 딤이 자유 건설을 막으므로,
    // 여기서 채워 준 것을 엉뚱한 데 써 버리고 다시 막히는 일이 없다.
    private void OnEnable()
    {
        if (_hasGranted)
        {
            return;
        }

        _hasGranted = true;

        // 채웠을 때만 알린다. 넉넉해서 아무것도 안 한 경우까지 말하면,
        // 제 힘으로 모아 온 플레이어에게 받지도 않은 것을 받았다고 하는 셈이 된다.
        bool hasGrantedResources = GrantMissingResources();
        bool hasGrantedPopulation = GrantMissingPopulation();

        if (_toast == null)
        {
            return;
        }

        // 둘 다면 두 줄이 차례로 뜬다(토스트가 큐를 돌린다) - 자원과 인구는 다른 이야기라 합치지 않는다.
        if (hasGrantedResources)
        {
            _toast.Show(RESOURCE_GRANTED_LOC_KEY);
        }

        if (hasGrantedPopulation)
        {
            _toast.Show(POPULATION_GRANTED_LOC_KEY);
        }
    }

    /// <summary>하나라도 채웠으면 true.</summary>
    private bool GrantMissingResources()
    {
        if (!WiringGuard.Require(_resourceManager, nameof(_resourceManager), this))
        {
            return false;
        }

        bool hasGranted = false;

        // Add(ResourceType, int)는 복합 플래그에 Assert가 걸리므로 종류별로 하나씩 넘긴다.
        foreach (ResourceAmount required in _requiredResources)
        {
            int missing = required.Amount - _resourceManager.GetAmount(required.Type);
            if (missing > 0)
            {
                _resourceManager.Add(required.Type, missing);
                hasGranted = true;
            }
        }

        return hasGranted;
    }

    // 배치해 둔 인구를 회수하지 않고 최대 인구를 올린다 - 회수하면 플레이어가 고른 배치가 말없이 바뀌고,
    // 그 건물의 산출이 떨어져 다음 날 정산이 안내와 어긋난다.
    /// <summary>늘렸으면 true.</summary>
    private bool GrantMissingPopulation()
    {
        if (!WiringGuard.Require(_populationManager, nameof(_populationManager), this))
        {
            return false;
        }

        int missing = _requiredIdlePopulation - _populationManager.AvailablePopulation;
        if (missing <= 0)
        {
            return false;
        }

        return _populationManager.TryIncreaseMaxPopulation(missing);
    }
}
