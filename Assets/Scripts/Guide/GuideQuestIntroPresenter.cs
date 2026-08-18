using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 조언자 카드를 런당 한 번 띄우고, 고른 선택지를 가이드 수준으로 옮긴다.
///
/// <b>가이드 수준과 무관하게 뜬다.</b> 이 카드가 수준을 고르는 창구이므로, "안내 없음"을 고른
/// 플레이어에게도 새 게임에서 한 번은 인사한다(이전에 고른 수준이 기본값으로 남아 있으므로
/// 같은 선택지를 다시 고르면 그만이다).
///
/// 답했다는 기록은 <b>선택지 콜백 안에서만</b> 남긴다. 창이 꺼져 카드가 답 없이 사라지는 경로가 있고
/// (UI_ConfirmNotificationToast.ShowChoices), 그때 플래그가 서 버리면 다시 물어볼 기회가 사라진다.
/// </summary>
public sealed class GuideQuestIntroPresenter : MonoBehaviour
{
    private const int FIRST_DAY_NUMBER = 1;

    [Tooltip("완료 기록과 보상 지급을 맡긴다.")]
    [SerializeField] private GuideQuestController _controller;

    [Tooltip("답했는지를 기록할 런.")]
    [SerializeField] private GameManager _gameManager;

    [Tooltip("1일차 시작을 듣는다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("조언자 카드를 띄울 우상단 알림.")]
    [SerializeField] private UI_ConfirmNotificationToast _toast;

    [Tooltip("고른 수준을 저장한다.")]
    [SerializeField] private SettingsService _settingsService;

    [Tooltip("띄울 퀘스트. 비우면 카탈로그의 조언자 퀘스트를 쓴다.")]
    [WiringOptional]
    [SerializeField] private GuideQuestSO _introQuest;

    [Tooltip("_introQuest를 비워 둘 때 조언자 퀘스트를 꺼내 올 카탈로그.")]
    [WiringOptional]
    [SerializeField] private GuideQuestCatalogSO _catalog;

    // 카드가 화면에 떠 있는 동안 다시 띄우지 않는다. 답하기 전에는 RunData의 플래그가 서지 않으므로
    // 이 세션 안의 중복 표시는 이 값으로 막는다.
    private bool _isCardOnScreen;

    private RunData Run => _gameManager == null ? null : _gameManager.CurrentRun;

    private GuideQuestSO IntroQuest =>
        _introQuest != null ? _introQuest : (_catalog == null ? null : _catalog.IntroQuest);

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.AddListener(HandleDayReady);
        }
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayReady.RemoveListener(HandleDayReady);
        }
    }

    /// <summary>
    /// OnDayReady를 고른 이유는 HelpDiscoveryController와 같다 - 새 게임(StartDay)·
    /// 이어하기(ResumeDay)·로드 실패 폴백(StartNewRun) 세 경로가 전부 여기를 지난다.
    /// 이어하기로 들어온 런은 이미 답한 상태이므로 아래 플래그에서 걸러진다.
    /// </summary>
    private void HandleDayReady(int dayNumber)
    {
        if (dayNumber != FIRST_DAY_NUMBER)
        {
            return;
        }

        TryShowIntro();
    }

    private void TryShowIntro()
    {
        GuideQuestSO quest = IntroQuest;

        if (_isCardOnScreen || quest == null || _toast == null || Run == null || Run.IsGuideIntroAnswered)
        {
            return;
        }

        if (quest.Choices.Count == 0)
        {
            Debug.LogError($"[GuideQuestIntroPresenter] {quest.name}에 선택지가 없어 수준을 고를 수 없습니다.", quest);
            return;
        }

        var options = new List<UI_ConfirmNotificationToast.ChoiceOption>();

        foreach (GuideQuestSO.Choice choice in quest.Choices)
        {
            GuideQuestSO.Choice captured = choice;
            options.Add(new UI_ConfirmNotificationToast.ChoiceOption(
                choice.LabelLocKey,
                () => HandleChoicePicked(quest, captured)));
        }

        _isCardOnScreen = true;
        _toast.ShowChoices(quest.BodyLocKey, options);
    }

    private void HandleChoicePicked(GuideQuestSO quest, GuideQuestSO.Choice choice)
    {
        _isCardOnScreen = false;

        if (choice.SetsGuideLevel && _settingsService != null)
        {
            _settingsService.SetGuideLevel(choice.GuideLevel);
        }

        // 답했다는 사실은 여기서만 남긴다 - 위 주석의 이유로 카드를 띄우는 시점에 세우면 안 된다.
        if (Run != null)
        {
            Run.IsGuideIntroAnswered = true;
        }

        // 선택지 보상은 퀘스트 완료 보상과 별개다. 완료가 중복 지급을 막아 주므로
        // 선택지 보상도 완료가 처음 기록될 때만 준다.
        if (_controller != null && _controller.TryCompleteQuest(quest))
        {
            _controller.GrantRewards(choice.Rewards);
        }
    }
}
