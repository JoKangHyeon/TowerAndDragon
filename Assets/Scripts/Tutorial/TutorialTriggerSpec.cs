using System;
using UnityEngine;

/// <summary>
/// 목표가 "무슨 일이 일어나면 완료인가"를 담는다. TutorialStepSO에서 조건 부분만 떼어낸 모양이지만
/// 단계 전체를 재사용하지는 않는다 - 단계에는 문구·앵커·입력 차단처럼 목표와 상관없는 것이 함께 붙어 있고,
/// 자유 목표에 그것들을 끌고 오면 "가리킬 대상이 없는 목표"라는 빈 필드가 계속 남는다.
///
/// 지원하는 조건은 <b>이벤트 한 번으로 판정되는 것</b>뿐이다. PopulationAssigned 계열은 단계 진입 시점
/// 대비 증가분으로 재는 구조라(TutorialRunner.CaptureConditionBaseline) 언제 시작했는지가 없는
/// 자유 목표에서는 의미가 서지 않는다.
/// </summary>
[Serializable]
public sealed class TutorialTriggerSpec
{
    [Tooltip("완료로 볼 사건. 목표에는 이벤트로 판정되는 조건만 쓴다 - 인구 배치 계열은 증가분 기준이라 맞지 않는다.")]
    [SerializeField] private TutorialConditionType _condition = TutorialConditionType.None;

    [Tooltip("ExclusiveModeOpened / DragonWindowMotherTabSelected 등 창을 보는 조건에서 기다릴 모드.")]
    [SerializeField] private TutorialExclusiveModeKind _targetMode = TutorialExclusiveModeKind.None;

    [Tooltip("BuildingConstructed 조건에서 기다릴 건물 종류.")]
    [SerializeField] private TutorialBuildingKind _targetBuilding = TutorialBuildingKind.None;

    [Tooltip("생산시설을 종류까지 좁힐 때만 넣는다(농장 vs 채석장). 비우면 아무 생산시설이나 통과한다.")]
    [SerializeField] private ResourceProductionData _targetFactoryData;

    public TutorialConditionType Condition => _condition;
    public TutorialExclusiveModeKind TargetMode => _targetMode;
    public TutorialBuildingKind TargetBuilding => _targetBuilding;
    public ResourceProductionData TargetFactoryData => _targetFactoryData;

    public bool MatchesBuilding(Building building) =>
        TutorialTargetMatcher.MatchesBuilding(building, _targetBuilding, _targetFactoryData);
}
