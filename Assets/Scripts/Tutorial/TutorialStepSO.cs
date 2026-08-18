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
    private const int MIN_REQUIRED_COUNT = 1;

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

    [Tooltip("용 창 새끼용 탭에 지금 떠 있는 첫 슬롯을 가리킨다. 건설 슬롯과 같은 이유로 앵커를 쓸 수 없다 - " +
             "알 목록과 새끼용 목록이 한 패널에 함께 있으므로 어느 쪽인지 단계가 정한다.")]
    [SerializeField] private TutorialDynamicTargetKind _dynamicTarget = TutorialDynamicTargetKind.None;

    // 딤과 대상 외 클릭 차단은 데이터로 두지 않는다 - UI_GuideOverlay가 대상·확인 버튼 유무로 스스로 정한다.
    // 단계마다 켜고 끄게 두었더니 빠뜨린 곳이 계속 나왔고, 그때마다 플레이어가 엉뚱한 버튼을 눌러
    // 안내가 가리키던 창이 닫혔다.

    [Tooltip("가리키기만 하고 대상 클릭은 막을지. 눌러보게 하는 게 아니라 '이런 게 있다'만 알리는 설명형에 쓴다. " +
             "되돌릴 수 없는 조작(하루 1회뿐인 어미용 속성 변경 등)을 설명 중에 소모하지 않게 한다.")]
    [SerializeField] private bool _blocksTargetInteraction;

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

    [Tooltip("BuildingCountReached 조건에서 서 있어야 할 건물 수. 증가분이 아니라 총량이다.")]
    [Min(MIN_REQUIRED_COUNT)]
    [SerializeField] private int _requiredCount = MIN_REQUIRED_COUNT;

    [Tooltip("BuildingCountReached 조건에서 정원을 채운 것만 셀지. 타워는 충원율이 곧 화력이라 " +
             "개수만 채운 것으로는 밤을 넘기는 기준이 되지 않는다.")]
    [SerializeField] private bool _requiresStaffed;

    [Tooltip("이 배타 창이 이미 열려 있으면 이 단계를 건너뛴다. 창으로 가는 통로를 시키는 단계에 쓴다 - " +
             "성을 클릭하게 하는 단계인데 플레이어가 이미 용 창에 들어가 있으면, 성 선택이 풀려 있어 " +
             "조건이 영영 거짓인 채로 딤만 남는다.")]
    [SerializeField] private TutorialExclusiveModeKind _skipIfModeOpen = TutorialExclusiveModeKind.None;

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
    public TutorialDynamicTargetKind DynamicTarget => _dynamicTarget;
    public bool BlocksTargetInteraction => _blocksTargetInteraction;
    public GuideBubbleSlot BubbleSlot => _bubbleSlot;
    public TutorialConditionType Condition => _condition;
    public TutorialExclusiveModeKind TargetMode => _targetMode;
    public TutorialBuildingKind TargetBuilding => _targetBuilding;
    public ResourceProductionData TargetFactoryData => _targetFactoryData;
    public int RequiredPopulation => _requiredPopulation;
    public int RequiredCount => _requiredCount;
    public bool RequiresStaffed => _requiresStaffed;
    public TutorialExclusiveModeKind SkipIfModeOpen => _skipIfModeOpen;
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

        // 문구가 없는 행동형은 '이음매' 컷이다 - 다른 안내가 화면을 쓰는 동안 조건만 기다린다
        // (TutorialRunner.Render 참고). 설명형은 문구가 곧 내용이므로 비면 뜻이 없다.
        if (string.IsNullOrWhiteSpace(_messageLocKey) && _kind == TutorialStepKind.Acknowledge)
        {
            Debug.LogWarning($"[TutorialStepSO] {name}: 설명형인데 문구 키(_messageLocKey)가 비어 있습니다.", this);
        }

        if (_kind == TutorialStepKind.WaitForAction && _condition == TutorialConditionType.None)
        {
            Debug.LogWarning(
                $"[TutorialStepSO] {name}: 행동형인데 완료 조건이 None이라 이 단계에서 멈춥니다.", this);
        }

        // 설명형을 행동형으로 바꿀 때 놓치기 쉬운 조합이다 - 대상 클릭을 막아둔 채로 그 대상을
        // 누르라고 하면 눌러도 반응이 없고, 행동형이라 확인 버튼도 없어 빠져나갈 길이 사라진다.
        if (_kind == TutorialStepKind.WaitForAction && _blocksTargetInteraction)
        {
            Debug.LogWarning(
                $"[TutorialStepSO] {name}: 행동형인데 대상 클릭을 막고 있습니다 - " +
                "그 대상을 눌러야 넘어가는 단계라면 갇힙니다.", this);
        }

        bool needsTargetMode = _condition == TutorialConditionType.ExclusiveModeOpened ||
                               _condition == TutorialConditionType.ExclusiveModeClosed;

        if (needsTargetMode && _targetMode == TutorialExclusiveModeKind.None)
        {
            Debug.LogWarning($"[TutorialStepSO] {name}: 기다릴 배타 모드를 지정하지 않았습니다.", this);
        }

        // 선택 해제·정원 충족은 무엇이 골라져 있는지로 판정하므로 건물 종류가 필요 없다.
        bool needsBuildingKind = _condition == TutorialConditionType.BuildingCountReached ||
                                 _condition == TutorialConditionType.BuildingConstructed ||
                                 _condition == TutorialConditionType.BuildingSelectedForPlacement ||
                                 _condition == TutorialConditionType.BuildingSelectedOnGrid ||
                                 _condition == TutorialConditionType.BuildingRemoved ||
                                 _condition == TutorialConditionType.BuildingMoved;

        if (needsBuildingKind && _targetBuilding == TutorialBuildingKind.None)
        {
            Debug.LogWarning($"[TutorialStepSO] {name}: 기다릴 건물 종류를 지정하지 않았습니다.", this);
        }

        // 설명형은 확인 버튼으로 넘어가므로 건너뛸 이유가 없다 - 설정해 두면 의도가 있는 것처럼 보여 헷갈린다.
        if (_kind == TutorialStepKind.Acknowledge && _skipIfModeOpen != TutorialExclusiveModeKind.None)
        {
            Debug.LogWarning(
                $"[TutorialStepSO] {name}: 설명형에는 건너뛸 배타 모드가 쓰이지 않습니다.", this);
        }
    }
}
