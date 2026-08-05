using UnityEngine;

/// <summary>
/// 안내 한 단계. 순서·문구·대상·완료조건 종류를 데이터로 담는다.
/// 새끼용 가이드는 4단계라 enum + switch가 맞았지만 1일차는 20단계가 넘고 순서·문구가 플레이테스트마다 바뀐다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Tutorial/Step", fileName = "TS_Step")]
public sealed class TutorialStepSO : ScriptableObject
{
    private const float DEFAULT_ACKNOWLEDGE_SECONDS = 4f;

    // 0을 허용하면 확인 버튼이 없는 지금은 그 단계에서 영영 멈춘다.
    private const float MIN_ACKNOWLEDGE_SECONDS = 0.5f;
    private const int MIN_REQUIRED_POPULATION = 1;

    [Tooltip("진행도 저장 키. 에셋 이름을 바꿔도 진행도가 유지되도록 따로 둔다. 시퀀스 안에서 중복되면 안 된다.")]
    [SerializeField] private string _stepId;

    [Tooltip("행동을 기다릴지(WaitForAction), 읽히고 넘어갈지(Acknowledge).")]
    [SerializeField] private TutorialStepKind _kind = TutorialStepKind.WaitForAction;

    [Tooltip("말풍선 문구의 스트링테이블 키.")]
    [SerializeField] private string _messageLocKey;

    [Tooltip("가리킬 대상. None이면 딤 없이 말풍선만 띄운다.")]
    [SerializeField] private GuideAnchorId _anchorId = GuideAnchorId.None;

    [Tooltip("건설 패널의 이 건물 슬롯을 가리킨다. 채우면 위 앵커보다 우선한다 - " +
             "슬롯은 런타임 생성이라 GuideAnchor를 붙일 수 없어 창에서 직접 찾아온다.")]
    [SerializeField] private Building _targetBuildingSlot;

    [Tooltip("대상 외 클릭을 막을지. 대상이 없으면 막을 수 없다 - 막으면 아무것도 누를 수 없게 된다.")]
    [SerializeField] private bool _blocksInput;

    [Tooltip("말풍선을 띄울 자리. 타일을 클릭해야 하는 단계는 말풍선이 그리드를 가리므로 Top/Bottom으로 옮긴다.")]
    [SerializeField] private GuideBubbleSlot _bubbleSlot = GuideBubbleSlot.Default;


    [Header("완료 조건 (WaitForAction 전용)")]
    [SerializeField] private TutorialConditionType _condition = TutorialConditionType.None;

    [Tooltip("ExclusiveModeOpened 조건에서 기다릴 모드.")]
    [SerializeField] private TutorialExclusiveModeKind _targetMode = TutorialExclusiveModeKind.None;

    [Tooltip("BuildingConstructed 조건에서 기다릴 건물 종류.")]
    [SerializeField] private TutorialBuildingKind _targetBuilding = TutorialBuildingKind.None;

    [Tooltip("생산시설을 종류까지 좁힐 때만 넣는다(농장 vs 채석장). 비우면 아무 생산시설이나 통과한다.")]
    [SerializeField] private ResourceProductionData _targetFactoryData;

    [Tooltip("PopulationAssigned 조건에서 이 단계 동안 새로 넣어야 할 인구 수. 총합이 아니라 증가분이다.")]
    [Min(MIN_REQUIRED_POPULATION)]
    [SerializeField] private int _requiredPopulation = MIN_REQUIRED_POPULATION;

    [Header("Acknowledge 전용")]
    [Tooltip("확인 버튼을 눌러 넘긴다. 읽는 속도는 사람마다 달라 설명형에는 이 방식을 권한다.")]
    [SerializeField] private bool _waitForConfirm = true;

    [Tooltip("확인 버튼을 쓰지 않을 때만 의미가 있다 - 이 시간(초)이 지나면 자동으로 넘어간다.")]
    [Min(MIN_ACKNOWLEDGE_SECONDS)]
    [SerializeField] private float _autoAdvanceSeconds = DEFAULT_ACKNOWLEDGE_SECONDS;

    public string StepId => _stepId;
    public TutorialStepKind Kind => _kind;
    public string MessageLocKey => _messageLocKey;
    public GuideAnchorId AnchorId => _anchorId;
    public Building TargetBuildingSlot => _targetBuildingSlot;
    public bool BlocksInput => _blocksInput;
    public GuideBubbleSlot BubbleSlot => _bubbleSlot;
    public TutorialConditionType Condition => _condition;
    public TutorialExclusiveModeKind TargetMode => _targetMode;
    public TutorialBuildingKind TargetBuilding => _targetBuilding;
    public ResourceProductionData TargetFactoryData => _targetFactoryData;
    public int RequiredPopulation => _requiredPopulation;
    public bool WaitForConfirm => _waitForConfirm;
    public float AutoAdvanceSeconds => _autoAdvanceSeconds;

    // 확인 버튼은 읽고 넘기는 설명에서만 띄운다 - 행동형에 있으면 행동을 건너뛰고 눌러버릴 수 있다.
    public bool ShowsConfirmButton => _kind == TutorialStepKind.Acknowledge && _waitForConfirm;

    // 데이터로 빠진 단계는 컴파일러가 잡아주지 않으므로, 스스로 넘어가지 못하는 조합을 에디터에서 알린다.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_stepId))
        {
            Debug.LogWarning($"[TutorialStepSO] {name}: 진행도 저장 키(_stepId)가 비어 있습니다.", this);
        }

        if (string.IsNullOrWhiteSpace(_messageLocKey))
        {
            Debug.LogWarning($"[TutorialStepSO] {name}: 문구 키(_messageLocKey)가 비어 있습니다.", this);
        }

        if (_blocksInput && _anchorId == GuideAnchorId.None)
        {
            Debug.LogWarning(
                $"[TutorialStepSO] {name}: 가릴 대상이 없는데 입력을 막으려 합니다 - 안내가 뜨지 않습니다.", this);
        }

        if (_kind == TutorialStepKind.WaitForAction && _condition == TutorialConditionType.None)
        {
            Debug.LogWarning(
                $"[TutorialStepSO] {name}: 행동형인데 완료 조건이 None이라 이 단계에서 멈춥니다.", this);
        }

        if (_condition == TutorialConditionType.ExclusiveModeOpened &&
            _targetMode == TutorialExclusiveModeKind.None)
        {
            Debug.LogWarning($"[TutorialStepSO] {name}: 기다릴 배타 모드를 지정하지 않았습니다.", this);
        }

        // 선택 해제·정원 충족은 무엇이 골라져 있는지로 판정하므로 건물 종류가 필요 없다.
        bool needsBuildingKind = _condition == TutorialConditionType.BuildingConstructed ||
                                 _condition == TutorialConditionType.BuildingSelectedForPlacement ||
                                 _condition == TutorialConditionType.BuildingSelectedOnGrid;

        if (needsBuildingKind && _targetBuilding == TutorialBuildingKind.None)
        {
            Debug.LogWarning($"[TutorialStepSO] {name}: 기다릴 건물 종류를 지정하지 않았습니다.", this);
        }
    }
}
