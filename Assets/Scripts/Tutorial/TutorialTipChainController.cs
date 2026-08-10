using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 목표를 향한 행동이 실제로 일어났을 때 그 주제의 안내를 열어 준다.
///
/// 1일차 강제 시퀀스와 다른 점은 "언제 여는가"뿐이다. 열고 나면 같은 <see cref="TutorialRunner"/>가
/// 같은 방식으로 단계를 진행한다 - 조건 판정·앵커·동적 대상이 전부 그대로다.
///
/// 대신 <b>관문을 걸지 않는다</b>(각 체인 러너의 _holdsGates를 꺼 둔다). 플레이어가 스스로 시작한
/// 안내이므로 도중에 그만두는 것도 자유여야 한다. 그만두면 러너는 그 단계에 머물고, 다시 그 화면으로
/// 돌아오면 이어서 진행된다.
///
/// 체인은 한 번에 하나만 연다. 농장과 슬라임 농장을 연달아 지으면 뒤엣것은 앞엣것이 끝난 뒤에 열린다.
/// </summary>
public sealed class TutorialTipChainController : MonoBehaviour
{
    private const int FIRST_DAY_NUMBER = 1;

    [Serializable]
    private sealed class Chain
    {
        [Tooltip("이 체인의 러너. 꺼진 채로 자기 차례를 기다리는 별도 오브젝트여야 한다.")]
        public TutorialRunner Runner;

        [Tooltip("무엇이 일어나면 이 체인을 여는가.")]
        public TutorialTriggerSpec Trigger = new();

        [Tooltip("이 일차부터 열린다. 앞당겨 해내면 그때 바로 열어도 되므로 대개 1로 둔다.")]
        [Min(FIRST_DAY_NUMBER)]
        public int RecommendedDay = FIRST_DAY_NUMBER;
    }

    [SerializeField] private List<Chain> _chains = new();

    [SerializeField] private GameManager _gameManager;
    [SerializeField] private CycleManager _cycleManager;

    [Header("트리거를 듣는 대상")]
    [SerializeField] private GridMap _gridMap;
    [SerializeField] private UIManager _uiManager;

    // 지금 돌고 있는 체인. 하나가 끝나야 다음이 열린다.
    private TutorialRunner _running;

    // 이미 한 번 연 체인. 다시 열지 않는다 - 러너는 진행도를 RunData에 남기므로 재활성하면
    // 마지막 단계 뒤에서 재개해 아무것도 안내하지 않은 채 끝난다.
    private readonly HashSet<TutorialRunner> _opened = new();

    private int CurrentDayNumber =>
        _cycleManager == null ? FIRST_DAY_NUMBER : _cycleManager.CurrentDayNumber;

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.AddListener(HandleBuildingAdded);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.AddListener(HandleExclusiveModeOpened);
        }

        // 체인 러너는 제 차례가 오기 전까지 꺼져 있어야 한다 - 켜져 있으면 시작하자마자 안내가 뜬다.
        foreach (Chain chain in _chains)
        {
            if (chain.Runner != null && chain.Runner.gameObject.activeSelf)
            {
                Debug.LogWarning(
                    $"[TutorialTipChainController] {chain.Runner.name}이 켜진 채로 저장돼 있어 끕니다 - " +
                    "체인 러너는 꺼진 상태로 두어야 합니다.", chain.Runner);
                chain.Runner.gameObject.SetActive(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (_gridMap != null)
        {
            _gridMap.OnBuildingAdded.RemoveListener(HandleBuildingAdded);
        }

        if (_uiManager != null)
        {
            _uiManager.ExclusiveModeOpened.RemoveListener(HandleExclusiveModeOpened);
        }

        foreach (Chain chain in _chains)
        {
            if (chain.Runner != null)
            {
                chain.Runner.TutorialEnded.RemoveListener(HandleChainEnded);
            }
        }
    }

    private void HandleBuildingAdded(Building building) =>
        TryOpen(TutorialConditionType.BuildingConstructed, chain => chain.Trigger.MatchesBuilding(building));

    private void HandleExclusiveModeOpened(MonoBehaviour mode) =>
        TryOpen(TutorialConditionType.ExclusiveModeOpened,
            chain => TutorialTargetMatcher.MatchesMode(mode, chain.Trigger.TargetMode));

    private void TryOpen(TutorialConditionType condition, Predicate<Chain> matches)
    {
        if (_running != null)
        {
            return;
        }

        foreach (Chain chain in _chains)
        {
            if (chain.Runner == null || _opened.Contains(chain.Runner))
            {
                continue;
            }

            if (chain.Trigger.Condition != condition || chain.RecommendedDay > CurrentDayNumber)
            {
                continue;
            }

            if (!matches(chain))
            {
                continue;
            }

            Open(chain);
            return;
        }
    }

    private void Open(Chain chain)
    {
        _opened.Add(chain.Runner);
        _running = chain.Runner;

        // 구독을 먼저 건다 - 빈 시퀀스는 켜지는 프레임에 끝나버려 종료를 놓친다.
        chain.Runner.TutorialEnded.AddListener(HandleChainEnded);
        chain.Runner.gameObject.SetActive(true);

        Debug.Log($"[TutorialTipChainController] 팁 체인 시작: {chain.Runner.name}", chain.Runner);
    }

    private void HandleChainEnded()
    {
        if (_running == null)
        {
            return;
        }

        _running.TutorialEnded.RemoveListener(HandleChainEnded);
        _running.gameObject.SetActive(false);
        Debug.Log($"[TutorialTipChainController] 팁 체인 종료: {_running.name}", _running);
        _running = null;
    }
}
