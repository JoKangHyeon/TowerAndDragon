using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 튜토리얼 챕터를 순서대로 넘긴다. 앞 챕터가 끝나면(TutorialEnded) 다음 챕터를 켜고 앞 챕터는 끈다.
///
/// 러너를 "날짜"가 아니라 "챕터"로 쪼개는 이유는 지급 때문이다 - 알 지급이나 연구용 자원 지급은
/// 시퀀스 한가운데서 일어나야 하는데, TutorialRunner는 시퀀스 전체가 끝날 때만 신호를 준다.
/// 챕터 경계로 쪼개면 그 이음매에 지급 컴포넌트를 인스펙터로 물릴 수 있어 코드를 고칠 필요가 없다
/// (BabyDragonTutorialHandOff가 이미 TutorialEnded를 구독하는 구조다).
///
/// 챕터마다 시작 일차를 두는 이유: 챕터가 끝나면 곧바로 다음 챕터가 이어지는데, 날짜가 바뀌어야
/// 성립하는 챕터(밤이 지나야 알이 부화하고 정산이 돈다)가 같은 낮에 시작되면 안 된다.
/// 시작 일차가 아직 오지 않은 챕터는 대기 상태로 두고, 그날 아침이 오면 그때 연다.
///
/// 각 챕터는 자기 TutorialRunner 하나만 가진 별도 오브젝트여야 한다 - 낮/밤 루프나 보스 같은
/// 상시 시스템과 같은 오브젝트에 두면 챕터를 끌 때 그것들까지 멈춘다.
/// </summary>
public sealed class TutorialScenarioController : MonoBehaviour
{
    private const int FIRST_CHAPTER_INDEX = 0;
    private const int FIRST_DAY_NUMBER = 1;

    [Serializable]
    private struct ChapterEntry
    {
        [Tooltip("이 챕터의 러너. 자기 TutorialRunner 하나만 가진 오브젝트여야 한다.")]
        public TutorialRunner Runner;

        [Tooltip("이 챕터가 시작될 수 있는 가장 이른 일차. 앞 챕터가 먼저 끝나도 이 날이 와야 열린다.")]
        [Min(FIRST_DAY_NUMBER)]
        public int StartDay;
    }

    [Tooltip("챕터 순서. 첫 챕터만 씬에서 활성으로 두고 나머지는 꺼 둔다 - 꺼 두어야 그 챕터의 Awake가 " +
             "제 차례가 오기 전에 돌지 않는다.")]
    [SerializeField] private List<ChapterEntry> _chapters = new();

    [Tooltip("날짜가 바뀌는 것을 보고 대기 중인 챕터를 여는 데 쓴다.")]
    [SerializeField] private CycleManager _cycleManager;

    private int _currentIndex = -1;

    // 차례는 됐지만 아직 그 일차가 오지 않아 열지 못한 챕터. -1이면 대기 중인 것이 없다.
    private int _pendingIndex = -1;

    // 챕터의 OnEnable이 거는 것(창 열기 제한·밤 시작 잠금)은 다른 오브젝트의 Start보다 앞서야 한다.
    // 그래서 Start가 아니라 Awake에서 첫 챕터를 연다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        EnterChapter(FIRST_CHAPTER_INDEX);
    }

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.AddListener(HandleDayStart);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(HandleDayStart);
        }

        // 씬이 내려갈 때 남의 오브젝트를 끄지 않는다 - 구독만 정리한다.
        if (TryGetRunner(_currentIndex, out TutorialRunner current))
        {
            current.TutorialEnded.RemoveListener(HandleChapterEnded);
        }
    }

    private void HandleDayStart(int dayNumber)
    {
        if (_pendingIndex >= 0 && _chapters[_pendingIndex].StartDay <= dayNumber)
        {
            int index = _pendingIndex;
            _pendingIndex = -1;
            OpenChapter(index);
        }
    }

    private void EnterChapter(int index)
    {
        if (TryGetRunner(_currentIndex, out TutorialRunner previous))
        {
            previous.TutorialEnded.RemoveListener(HandleChapterEnded);
            previous.gameObject.SetActive(false);
        }

        _currentIndex = index;

        // 빈 칸에서 멈추면 그 뒤 챕터가 전부 사라지므로 건너뛴다.
        while (index < _chapters.Count && _chapters[index].Runner == null)
        {
            Debug.LogWarning($"[TutorialScenarioController] {index}번째 챕터가 비어 있어 건너뜁니다.", this);
            index++;
            _currentIndex = index;
        }

        if (index >= _chapters.Count)
        {
            return;
        }

        // 아직 그 일차가 오지 않았으면 열지 않고 기다린다. 이 동안은 어느 챕터도 돌지 않으므로
        // 밤 시작 잠금이 풀려 플레이어가 밤으로 넘어갈 수 있다 - 그게 다음 챕터를 여는 경로다.
        int currentDay = _cycleManager == null ? FIRST_DAY_NUMBER : _cycleManager.CurrentDayNumber;

        if (_chapters[index].StartDay > currentDay)
        {
            _pendingIndex = index;
            return;
        }

        OpenChapter(index);
    }

    private void OpenChapter(int index)
    {
        if (!TryGetRunner(index, out TutorialRunner chapter))
        {
            return;
        }

        // 구독을 먼저 건다 - 켜는 순간 끝나버리는 챕터(빈 시퀀스)의 종료를 놓치지 않기 위해서다.
        chapter.TutorialEnded.AddListener(HandleChapterEnded);
        chapter.gameObject.SetActive(true);

        // 아직 스텝이 없는 챕터는 켜진 프레임에 곧바로 끝나 계층 창에서 전환을 눈으로 잡을 수 없다.
        Debug.Log($"[TutorialScenarioController] 챕터 {index} 진입: {chapter.name}", chapter);
    }

    private void HandleChapterEnded()
    {
        EnterChapter(_currentIndex + 1);
    }

    private bool TryGetRunner(int index, out TutorialRunner runner)
    {
        runner = index >= 0 && index < _chapters.Count ? _chapters[index].Runner : null;
        return runner != null;
    }

    /// <summary>[테스트 전용] 지금 돌고 있는 챕터. 대기 중(그 일차가 오지 않음)이면 null.</summary>
    public TutorialRunner DebugCurrentRunner =>
        TryGetRunner(_currentIndex, out TutorialRunner runner) ? runner : null;

    /// <summary>[테스트 전용] 지금 챕터의 시작 일차. 챕터가 없으면 0.</summary>
    public int DebugCurrentChapterStartDay =>
        _currentIndex >= 0 && _currentIndex < _chapters.Count ? _chapters[_currentIndex].StartDay : 0;

    /// <summary>[테스트 전용] 다음 챕터의 시작 일차. 남은 챕터가 없으면 0.</summary>
    public int DebugNextChapterStartDay
    {
        get
        {
            int next = _currentIndex + 1;
            return next >= 0 && next < _chapters.Count ? _chapters[next].StartDay : 0;
        }
    }

    /// <summary>[테스트 전용] 아직 열지 못하고 대기 중인 챕터가 있는지. 밤을 넘겨야 열린다.</summary>
    public bool DebugHasPendingChapter => _pendingIndex >= 0;
}
