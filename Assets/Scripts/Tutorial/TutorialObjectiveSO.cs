using UnityEngine;

/// <summary>
/// 자유 목표 하나. 강제 단계(TutorialStepSO)와 달리 순서가 없고, 완료하지 않아도 진행을 막지 않는다.
/// 권장 일차가 지나도 사라지지 않고 다음 날로 이월된다 - 못 한 것을 놓쳤다고 알리는 것이 아니라
/// 아직 남았다고 보여주는 목록이기 때문이다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Tutorial/Objective", fileName = "TO_Objective")]
public sealed class TutorialObjectiveSO : ScriptableObject
{
    private const int FIRST_DAY_NUMBER = 1;

    [Tooltip("완료 기록 키. 에셋 이름을 바꿔도 진행도가 유지되도록 따로 둔다. 목록 안에서 중복되면 안 된다.")]
    [SerializeField] private string _objectiveId;

    [Tooltip("목록에 표시할 한 줄 문구의 스트링테이블 키.")]
    [SerializeField] private string _titleLocKey;

    [Tooltip("보충 설명의 스트링테이블 키. 비워도 된다 - 한 줄로 충분한 목표가 대부분이다.")]
    [SerializeField] private string _descriptionLocKey;

    [Tooltip("이 목표를 목록에 띄우기 시작할 일차. 지났다고 사라지지는 않는다.")]
    [Min(FIRST_DAY_NUMBER)]
    [SerializeField] private int _recommendedDay = FIRST_DAY_NUMBER;

    [SerializeField] private TutorialTriggerSpec _completionTrigger = new();

    public string ObjectiveId => _objectiveId;
    public string TitleLocKey => _titleLocKey;
    public string DescriptionLocKey => _descriptionLocKey;
    public int RecommendedDay => _recommendedDay;
    public TutorialTriggerSpec CompletionTrigger => _completionTrigger;

    // 데이터로 빠진 목표는 컴파일러가 잡아주지 않는다 - 조용히 영영 완료되지 않는 조합을 에디터에서 알린다.
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_objectiveId))
        {
            Debug.LogWarning($"[TutorialObjectiveSO] {name}: 완료 기록 키(_objectiveId)가 비어 있습니다.", this);
        }

        if (string.IsNullOrWhiteSpace(_titleLocKey))
        {
            Debug.LogWarning($"[TutorialObjectiveSO] {name}: 문구 키(_titleLocKey)가 비어 있습니다.", this);
        }

        if (_completionTrigger == null || _completionTrigger.Condition == TutorialConditionType.None)
        {
            Debug.LogWarning($"[TutorialObjectiveSO] {name}: 완료 조건이 None이라 영영 완료되지 않습니다.", this);
            return;
        }

        bool needsBuildingKind = _completionTrigger.Condition == TutorialConditionType.BuildingConstructed;
        if (needsBuildingKind && _completionTrigger.TargetBuilding == TutorialBuildingKind.None)
        {
            Debug.LogWarning($"[TutorialObjectiveSO] {name}: 기다릴 건물 종류를 지정하지 않았습니다.", this);
        }

        bool needsTargetMode = _completionTrigger.Condition == TutorialConditionType.ExclusiveModeOpened ||
                               _completionTrigger.Condition == TutorialConditionType.ExclusiveModeClosed;

        if (needsTargetMode && _completionTrigger.TargetMode == TutorialExclusiveModeKind.None)
        {
            Debug.LogWarning($"[TutorialObjectiveSO] {name}: 기다릴 배타 모드를 지정하지 않았습니다.", this);
        }
    }
}
