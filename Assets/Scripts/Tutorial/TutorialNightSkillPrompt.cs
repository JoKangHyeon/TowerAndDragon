using UnityEngine;

/// <summary>
/// 보스 밤에 성이 깎이면 액티브 스킬 칸을 가리켜 한 번 써 보게 한다.
///
/// <b>목표(TutorialObjectiveSO)도 챕터 단계(TutorialStepSO)도 아니다.</b> 목표로 두면 밤 관문에 걸려
/// "밤에 해야 하는 일"이 밤으로 넘어가는 조건이 되고, 챕터 단계로 두면 챕터가 도는 동안
/// TutorialRunner가 밤 진입을 막아(<c>CanEndDay() =&gt; !_isRunning</c>) 그 밤이 시작되지 않는다.
/// 그래서 안내 오버레이의 제공자만 직접 구현한다 - BabyDragonGuideController와 같은 자리다.
///
/// 뜨는 조건을 전부 "지금 상태"로 읽는 이유: 밤 전투 중에는 성 체력이 오르내리고 스킬도 쿨타임을 도는데,
/// 한 번 켜고 끄는 플래그로 만들면 쓸 수 없는 칸을 가리킨 채 굳는다.
/// </summary>
public sealed class TutorialNightSkillPrompt : MonoBehaviour, IGuideRequestProvider
{
    // "성이 깎이고 있습니다. 스킬로 성을 회복하세요."
    private const string PROMPT_LOC_KEY = "tutorial_day3_use_skill";

    private const float DEFAULT_HEALTH_RATIO_THRESHOLD = 0.7f;
    private const int DEFAULT_PROMPT_DAY_NUMBER = 3;
    private const int FIRST_DAY_NUMBER = 1;

    [Tooltip("이 안내를 걸어 둘 오버레이.")]
    [SerializeField] private UI_GuideOverlay _overlay;

    [Tooltip("밤인지, 몇 일차인지 판정한다.")]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("체력 비율을 읽을 성.")]
    [SerializeField] private Castle _castle;

    [Tooltip("가리킬 스킬이 지금 쓸 수 있는지 확인하는 데 쓴다.")]
    [SerializeField] private SkillManager _skillManager;

    [Tooltip("써 보게 할 스킬. 비우면 안내가 뜨지 않는다 - 해금하지 않은 칸을 가리키지 않기 위해서다.")]
    [SerializeField] private SkillSO _promptSkill;

    [Tooltip("이 일차의 밤에만 뜬다.")]
    [Min(FIRST_DAY_NUMBER)]
    [SerializeField] private int _promptDayNumber = DEFAULT_PROMPT_DAY_NUMBER;

    [Tooltip("성 체력이 최대치의 이 비율 이하로 떨어지면 안내가 뜬다. " +
             "TutorialCastleGuard가 구제에 들어가는 비율보다 넉넉히 위여야 써 볼 틈이 있다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _healthRatioThreshold = DEFAULT_HEALTH_RATIO_THRESHOLD;

    // 한 밤에 한 번만 가르친다. 쿨타임이 돌아와도 다시 띄우면 보스전 내내 딤이 깜빡인다.
    private bool _hasCastThisNight;

    // 발동 순간을 직접 알려주는 이벤트가 없어(공용 스크립트를 건드리지 않는다) 사용 가능 여부의
    // true→false 전이로 읽는다. 쿨타임에 들어갔다는 것은 방금 썼다는 뜻이다.
    private bool _wasSkillUsable;

    private bool IsPromptNight =>
        _cycleManager != null &&
        _cycleManager.CurrentCycle == CycleManager.CycleState.Night &&
        _cycleManager.CurrentDayNumber == _promptDayNumber;

    private bool IsCastleHurt
    {
        get
        {
            if (_castle == null || _castle.MaxHealth <= 0f)
            {
                return false;
            }

            return _castle.CurrentHealth / _castle.MaxHealth <= _healthRatioThreshold;
        }
    }

    int IGuideRequestProvider.Priority => GuidePriority.NIGHT_SKILL_PROMPT;

    private void OnEnable()
    {
        if (_overlay != null)
        {
            _overlay.AddProvider(this);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.AddListener(HandleNightStart);
        }
    }

    private void OnDisable()
    {
        if (_overlay != null)
        {
            _overlay.RemoveProvider(this);
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
        }
    }

    // 배선이 빠지면 안내가 조용히 영영 뜨지 않는다 - 밤에만 나타나는 것이라 눈으로는 잡기 어렵다.
    private void Start()
    {
        if (_overlay == null || _cycleManager == null || _castle == null ||
            _skillManager == null || _promptSkill == null)
        {
            Debug.LogWarning("[TutorialNightSkillPrompt] 참조가 비어 밤 스킬 안내가 뜨지 않습니다.", this);
        }
    }

    private void HandleNightStart(int _)
    {
        _hasCastThisNight = false;
        _wasSkillUsable = false;
    }

    // 상태 전이는 여기서만 한다 - TryGetRequest는 오버레이가 한 프레임에 여러 번 부를 수 있어,
    // 거기서 바꾸면 "누가 그리는가"를 묻는 것만으로 안내가 끝나 버린다.
    private void Update()
    {
        if (!IsPromptNight)
        {
            return;
        }

        Skill skill = FindPromptSkill();
        if (skill == null)
        {
            return;
        }

        if (_wasSkillUsable && !skill.CanUse)
        {
            _hasCastThisNight = true;
        }

        _wasSkillUsable = skill.CanUse;
    }

    /// <summary>
    /// 지금 낼 안내. 낼 것이 없으면 false를 돌려 화면을 넘긴다 - 우선순위가 가장 낮으므로
    /// 붙들고 있으면 위의 안내가 아니라 아무것도 뜨지 않는다.
    /// </summary>
    bool IGuideRequestProvider.TryGetRequest(out GuideRequest request)
    {
        request = GuideRequest.Hidden;

        if (_hasCastThisNight || !IsPromptNight || !IsCastleHurt)
        {
            return false;
        }

        // 쓸 수 없는 칸을 가리키면 시킨 대로 해도 아무 일이 없다. 쿨타임·해금 어느 쪽이든 같다.
        Skill skill = FindPromptSkill();
        if (skill == null || !skill.CanUse)
        {
            return false;
        }

        // 칸이 등록돼 있지 않으면(낮이거나 HUD가 꺼져 있으면) 가리킬 곳이 없다.
        if (!GuideAnchorRegistry.TryGet(GuideAnchorId.NightSkillSlot, out RectTransform anchor))
        {
            return false;
        }

        // 확인 버튼을 두지 않는다 - 눌러서 넘기는 안내가 아니라 스킬을 쓰면 사라지는 안내다.
        // 스킬은 Instant라 칸을 한 번 누르면 끝나므로, 구멍 하나만 뚫어도 시킨 것을 할 수 있다.
        request = GuideRequest.Draw(
            anchor,
            PROMPT_LOC_KEY,
            blocksTargetInteraction: false,
            keepsInputOpen: false,
            showsConfirmButton: false,
            GuideBubbleSlot.Bottom,
            null);

        return true;
    }

    // 오버레이가 화면을 쥔 제공자에게만 보내므로 남의 클릭이 섞이지는 않는다.
    // 확인 버튼을 내지 않으니 여기로 올 일은 없다.
    void IGuideRequestProvider.OnConfirmClicked()
    {
    }

    // 막힌 곳을 눌렀다고 잔소리하지 않는다 - 보스전 중이라 화면에 이미 할 말이 많다.
    void IGuideRequestProvider.OnBlockedClicked()
    {
    }

    private Skill FindPromptSkill()
    {
        if (_skillManager == null || _promptSkill == null)
        {
            return null;
        }

        foreach (Skill skill in _skillManager.AvailableSkills)
        {
            if (skill.Data == _promptSkill)
            {
                return skill;
            }
        }

        return null;
    }
}
