using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가이드 퀘스트 하나. 자유 목표(TutorialObjectiveSO)와 모양은 닮았지만 목적이 다르다 -
/// 저쪽은 튜토리얼 안에서 "해 볼 것"을 늘어놓고, 이쪽은 튜토리얼을 마친 플레이어에게
/// <b>극초반 빌드업에서 오늘 안에 끝내야 하는 것</b>을 알린다.
///
/// 그래서 여기에만 있는 것이 둘이다.
/// - <see cref="IsCoreStep"/>: 그날 안에 끝내야 하는 항목. 권장 일차가 지나면 목록에 지각 표시가 붙는다.
///   강제하지 않는 대신 놓친 것이 계속 보이게 하는 유일한 장치다.
/// - <see cref="Choices"/>: 선택지가 있는 카드. 조언자 퀘스트가 가이드 수준을 여기서 받는다.
///
/// 문구는 조작이 아니라 <b>이유와 기한</b>을 쓴다("연구소에 인구를 넣으세요"가 아니라
/// "RP는 밤이 끝날 때 지급됩니다 - 오늘 넣어야 내일 아침에 연구합니다").
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Guide/Quest", fileName = "GQ_Quest")]
public sealed class GuideQuestSO : ScriptableObject
{
    private const int FIRST_DAY_NUMBER = 1;
    private const int MIN_REQUIRED_COUNT = 1;

    /// <summary>
    /// 카드에 놓는 선택지 하나. 비어 있으면 확인 버튼 한 개짜리 보통 카드가 된다.
    /// </summary>
    [Serializable]
    public sealed class Choice
    {
        [Tooltip("버튼에 쓸 문구의 스트링테이블 키.")]
        [SerializeField] private string _labelLocKey;

        [Tooltip("이 선택지가 가이드 수준을 바꾸는가. 조언자 퀘스트에서만 켠다.")]
        [SerializeField] private bool _setsGuideLevel;

        [Tooltip("위 스위치를 켰을 때 적용할 수준. GuideLevel에는 \"안 바꿈\" 값이 없어 스위치가 따로 필요하다.")]
        [SerializeField] private GuideLevel _guideLevel = GuideLevel.Full;

        [Tooltip("이 선택지를 고르면 주는 자원. 대개 비운다.")]
        [SerializeField] private List<ResourceAmount> _rewards = new();

        public string LabelLocKey => _labelLocKey;
        public bool SetsGuideLevel => _setsGuideLevel;
        public GuideLevel GuideLevel => _guideLevel;
        public IReadOnlyList<ResourceAmount> Rewards => _rewards;
    }

    [Tooltip("완료 기록 키. 에셋 이름을 바꿔도 진행도가 유지되도록 따로 둔다. 목록 안에서 중복되면 안 된다.")]
    [SerializeField] private string _questId;

    [Tooltip("목록에 표시할 한 줄 문구의 스트링테이블 키.")]
    [SerializeField] private string _titleLocKey;

    [Tooltip("전체 수준에서 우상단 카드에 띄울 본문의 스트링테이블 키.")]
    [SerializeField] private string _bodyLocKey;

    [Tooltip("Tip만 수준에서 한 줄 토스트로 띄울 문구의 키. 비우면 제목을 대신 쓴다.")]
    [SerializeField] private string _tipLocKey;

    [Tooltip("설명 뒤에 지금 값을 덧붙일지. 시점에 따라 달라지는 숫자는 문안에 적지 말고 이걸 쓴다.")]
    [SerializeField] private GuideQuestBodyArgument _bodyArgument = GuideQuestBodyArgument.None;

    [Tooltip("설명의 {1} 자리에 넣을 인라인 아이콘 이름(BuildingIcons 스프라이트 에셋의 이름). " +
             "비우면 아이콘 없이 빈 자리가 된다. 예: farm_field")]
    [SerializeField] private string _bodyIconName;

    [Tooltip("이 목표를 목록에 띄우기 시작할 일차. 지났다고 사라지지는 않는다.")]
    [Min(FIRST_DAY_NUMBER)]
    [SerializeField] private int _recommendedDay = FIRST_DAY_NUMBER;

    [Tooltip("그날 안에 끝내야 하는 항목인가. 켜면 권장 일차가 지나도록 미완료일 때 목록에 지각 표시가 붙는다.")]
    [SerializeField] private bool _isCoreStep = true;

    [Tooltip("무엇이 일어나면 완료인가. 튜토리얼과 조건 카탈로그를 공유한다.")]
    [SerializeField] private TutorialTriggerSpec _completionTrigger = new();

    [Tooltip("완료에 필요한 개수. 타워 4채처럼 여러 개가 필요한 항목에만 올린다.")]
    [Min(MIN_REQUIRED_COUNT)]
    [SerializeField] private int _requiredCount = MIN_REQUIRED_COUNT;

    [Tooltip("선택지. 비우면 확인 버튼 하나짜리 카드가 된다.")]
    [SerializeField] private List<Choice> _choices = new();

    [Tooltip("완료하면 주는 자원. 조언자 퀘스트 외에는 비운다.")]
    [SerializeField] private List<ResourceAmount> _rewards = new();

    [Tooltip("전체 수준에서 말풍선으로 가리킬 UI. None이면 강조하지 않는다.")]
    [SerializeField] private GuideAnchorId _anchorId = GuideAnchorId.None;

    public string QuestId => _questId;
    public string TitleLocKey => _titleLocKey;
    public string BodyLocKey => _bodyLocKey;
    public GuideQuestBodyArgument BodyArgument => _bodyArgument;
    public string BodyIconName => _bodyIconName;
    public int RecommendedDay => _recommendedDay;
    public bool IsCoreStep => _isCoreStep;
    public TutorialTriggerSpec CompletionTrigger => _completionTrigger;
    public int RequiredCount => _requiredCount;
    public IReadOnlyList<Choice> Choices => _choices;
    public IReadOnlyList<ResourceAmount> Rewards => _rewards;
    public GuideAnchorId AnchorId => _anchorId;

    /// <summary>Tip 문구가 따로 없으면 제목을 쓴다 - 대부분의 퀘스트는 한 줄이면 충분하다.</summary>
    public string TipLocKey => string.IsNullOrWhiteSpace(_tipLocKey) ? _titleLocKey : _tipLocKey;

    // 데이터로 빠진 퀘스트는 컴파일러가 잡아주지 않는다 - 조용히 영영 완료되지 않는 조합을 에디터에서 알린다.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_questId))
        {
            Debug.LogWarning($"[GuideQuestSO] {name}: 완료 기록 키(_questId)가 비어 있습니다.", this);
        }

        if (string.IsNullOrWhiteSpace(_titleLocKey))
        {
            Debug.LogWarning($"[GuideQuestSO] {name}: 목록 문구 키(_titleLocKey)가 비어 있습니다.", this);
        }

        ValidateChoices();
        ValidateTrigger();
    }

    // 선택지로 끝나는 퀘스트는 조건이 없어도 된다(고르는 순간이 곧 완료다).
    // 그 외에는 조건이 None이면 목록에 뜬 채로 영영 남는다.
    private void ValidateTrigger()
    {
        if (_completionTrigger == null)
        {
            return;
        }

        if (_completionTrigger.Condition == TutorialConditionType.None)
        {
            if (_choices.Count == 0)
            {
                Debug.LogWarning($"[GuideQuestSO] {name}: 완료 조건이 None이고 선택지도 없어 영영 완료되지 않습니다.", this);
            }

            return;
        }

        if (_requiredCount < MIN_REQUIRED_COUNT)
        {
            Debug.LogWarning($"[GuideQuestSO] {name}: 필요 개수는 {MIN_REQUIRED_COUNT} 이상이어야 합니다.", this);
        }

        // 건물을 보는 조건은 종류가 None이면 TutorialTargetMatcher가 무엇도 통과시키지 않는다 -
        // 목록에 뜬 채로 영영 완료되지 않는다.
        bool needsBuildingKind =
            _completionTrigger.Condition == TutorialConditionType.BuildingConstructed ||
            _completionTrigger.Condition == TutorialConditionType.PopulationAssignedToBuilding ||
            _completionTrigger.Condition == TutorialConditionType.SelectedBuildingFullyStaffed ||
            _completionTrigger.Condition == TutorialConditionType.BuildingRemoved ||
            _completionTrigger.Condition == TutorialConditionType.BuildingMoved;

        if (needsBuildingKind && _completionTrigger.TargetBuilding == TutorialBuildingKind.None)
        {
            Debug.LogWarning($"[GuideQuestSO] {name}: 기다릴 건물 종류를 지정하지 않았습니다.", this);
        }

        bool needsResource =
            _completionTrigger.Condition == TutorialConditionType.ResourceGained ||
            _completionTrigger.Condition == TutorialConditionType.ResourceForecastNonNegative;

        if (needsResource && _completionTrigger.TargetResource == ResourceType.None)
        {
            Debug.LogWarning($"[GuideQuestSO] {name}: 기다릴 자원(TargetResource)을 지정하지 않았습니다.", this);
        }

        bool needsTargetMode =
            _completionTrigger.Condition == TutorialConditionType.ExclusiveModeOpened ||
            _completionTrigger.Condition == TutorialConditionType.ExclusiveModeClosed;

        if (needsTargetMode && _completionTrigger.TargetMode == TutorialExclusiveModeKind.None)
        {
            Debug.LogWarning($"[GuideQuestSO] {name}: 기다릴 배타 모드를 지정하지 않았습니다.", this);
        }
    }

    private void ValidateChoices()
    {
        foreach (Choice choice in _choices)
        {
            if (choice != null && string.IsNullOrWhiteSpace(choice.LabelLocKey))
            {
                Debug.LogWarning($"[GuideQuestSO] {name}: 문구 키가 빈 선택지가 있습니다.", this);
            }
        }
    }
}
