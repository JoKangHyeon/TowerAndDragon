using UnityEngine;

/// <summary>
/// 도감 항목 하나. 최초 조우 팝업과 도감 본문이 같은 데이터를 본다 -
/// 팝업용·도감용 문구를 따로 두면 유지보수가 두 배가 되는데, 팝업 본문이 3줄로 묶여 있어
/// 어차피 길이 상한이 같다. 단, 그림은 예외다 - <see cref="CodexIllustration"/> 참고.
///
/// 완료 기록을 에셋 이름과 분리하는 이유는 TutorialObjectiveSO._objectiveId와 같다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Help/Entry", fileName = "HE_Entry")]
public sealed class HelpEntrySO : ScriptableObject
{
    [Tooltip("해금 기록 키. 플레이어 프로필에 이 문자열 그대로 저장된다 - " +
             "한 번 정하면 절대 바꾸지 않는다. 바꾸면 기존 플레이어의 해금 이력이 끊긴다.")]
    [SerializeField] private string _entryId;

    [Tooltip("목록에서 묶일 갈래.")]
    [SerializeField] private HelpCategory _category = HelpCategory.Basics;

    [Tooltip("목록 한 줄 · 팝업 제목 · 본문 헤더가 함께 쓰는 스트링테이블 키.")]
    [SerializeField] private string _titleLocKey;

    [Tooltip("본문의 스트링테이블 키. 팝업도 같은 키를 쓰므로 3줄 안에 끝낸다.")]
    [SerializeField] private string _bodyLocKey;

    [Tooltip("목록 줄에 붙일 작은 아이콘(1:1). 비우면 아이콘 칸을 숨긴다.")]
    [WiringOptional]
    [SerializeField] private Sprite _icon;

    [Tooltip("팝업·도감 본문에 띄울 그림 1장. 비우면 텍스트만 나온다 - 아트가 나오기 전까지는 비워 둔다.")]
    [WiringOptional]
    [SerializeField] private Sprite _illustration;

    [Tooltip("도감 본문에만 띄울 그림. 최초 조우 팝업은 이 값을 보지 않는다 - 팝업은 본문이 3줄로 " +
             "묶여 있어 큰 그림이 붙으면 읽는 흐름이 끊긴다. 비우면 _illustration으로 되돌아간다.")]
    [WiringOptional]
    [SerializeField] private Sprite _codexIllustration;

    [Tooltip("같은 갈래 안에서의 정렬 순서. 카탈로그 리스트 순서에 기대지 않는다 - " +
             "항목을 중간에 끼울 때마다 리스트를 재정렬하면 머지 충돌이 난다.")]
    [SerializeField] private int _sortOrder;

    [Tooltip("이 항목을 해금할 사건.")]
    [SerializeField] private TutorialTriggerSpec _unlockTrigger = new();

    [Tooltip("조작키·낮밤처럼 처음부터 열려 있어야 하는 항목. 켜면 조건 없이 목록에 있고 팝업도 뜨지 않는다.")]
    [SerializeField] private bool _unlockedFromStart;

    [Tooltip("끄면 팝업 없이 도감에만 조용히 등록된다.")]
    [SerializeField] private bool _autoPopup = true;

    public string EntryId => _entryId;
    public HelpCategory Category => _category;
    public string TitleLocKey => _titleLocKey;
    public string BodyLocKey => _bodyLocKey;
    public Sprite Icon => _icon;
    public Sprite Illustration => _illustration;

    /// <summary>
    /// 도감 본문에 띄울 그림. 도감 전용 값이 있으면 그것을, 없으면 팝업과 공용인 <see cref="Illustration"/>을
    /// 쓴다 - 나중에 팝업용 아트가 생겨도 도감이 자동으로 따라간다.
    /// </summary>
    public Sprite CodexIllustration => _codexIllustration != null ? _codexIllustration : _illustration;

    public int SortOrder => _sortOrder;
    public TutorialTriggerSpec UnlockTrigger => _unlockTrigger;
    public bool UnlockedFromStart => _unlockedFromStart;
    public bool AutoPopup => _autoPopup;

    // 데이터로 빠진 항목은 컴파일러가 잡아주지 않는다 - 영영 해금되지 않는 조합을 에디터에서 알린다.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_entryId))
        {
            Debug.LogWarning($"[HelpEntrySO] {name}: 해금 기록 키(_entryId)가 비어 있습니다.", this);
        }

        if (string.IsNullOrWhiteSpace(_titleLocKey))
        {
            Debug.LogWarning($"[HelpEntrySO] {name}: 제목 키(_titleLocKey)가 비어 있습니다.", this);
        }

        if (string.IsNullOrWhiteSpace(_bodyLocKey))
        {
            Debug.LogWarning($"[HelpEntrySO] {name}: 본문 키(_bodyLocKey)가 비어 있습니다.", this);
        }

        // 처음부터 열려 있는 항목은 조건이 없는 것이 정상이다.
        if (_unlockedFromStart)
        {
            return;
        }

        if (_unlockTrigger == null || _unlockTrigger.Condition == TutorialConditionType.None)
        {
            Debug.LogWarning($"[HelpEntrySO] {name}: 해금 조건이 None이라 영영 해금되지 않습니다.", this);
            return;
        }

        bool needsBuildingKind = _unlockTrigger.Condition == TutorialConditionType.BuildingConstructed;
        if (needsBuildingKind && _unlockTrigger.TargetBuilding == TutorialBuildingKind.None)
        {
            Debug.LogWarning($"[HelpEntrySO] {name}: 기다릴 건물 종류를 지정하지 않았습니다.", this);
        }

        bool needsTargetMode = _unlockTrigger.Condition == TutorialConditionType.ExclusiveModeOpened ||
                               _unlockTrigger.Condition == TutorialConditionType.ExclusiveModeClosed;

        if (needsTargetMode && _unlockTrigger.TargetMode == TutorialExclusiveModeKind.None)
        {
            Debug.LogWarning($"[HelpEntrySO] {name}: 기다릴 배타 모드를 지정하지 않았습니다.", this);
        }

        bool needsResource = _unlockTrigger.Condition == TutorialConditionType.ResourceGained;
        if (needsResource && _unlockTrigger.TargetResource == ResourceType.None)
        {
            Debug.LogWarning($"[HelpEntrySO] {name}: 기다릴 자원 종류를 지정하지 않았습니다.", this);
        }

        // 지형 필터는 청크를 고르는 조건에서만 읽힌다. 다른 조건에 켜 두면 의도가 있는 것처럼
        // 보여 헷갈리고, 지형이 맞을 때만 뜨기를 기대한 항목이 아무 때나 뜬다.
        if (_unlockTrigger.FilterByTerrain &&
            _unlockTrigger.Condition != TutorialConditionType.ConquestChunkSelected)
        {
            Debug.LogWarning(
                $"[HelpEntrySO] {name}: 지형 필터를 켰지만 조건이 ConquestChunkSelected가 아니라 쓰이지 않습니다.",
                this);
        }
    }
}
