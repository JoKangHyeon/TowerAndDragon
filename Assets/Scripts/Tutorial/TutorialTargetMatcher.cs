using UnityEngine;

/// <summary>
/// "지금 일어난 일이 우리가 기다리던 대상인가"를 판정한다. 게임 이벤트는 전부 범용이라
/// (GridMap.OnBuildingAdded는 "아무 건물이나 지어짐", UIManager.ExclusiveModeOpened는 "아무 창이나 열림")
/// 종류를 좁히는 일은 구독자 몫인데, 그 구독자가 선형 시퀀스(TutorialRunner)와 자유 목표
/// (TutorialObjectiveController) 둘이 됐다. 같은 판정을 양쪽에 복제하면 한쪽만 고쳐져 갈라진다.
/// </summary>
public static class TutorialTargetMatcher
{
    // BabyDragonInventory와 DragonSkill이 같은 창을 가리키는 이유: 새끼용 인벤토리와 어미용 스킬트리가
    // UI_DragonWindow의 두 탭으로 합쳐졌다. 둘 다 UI_DragonWindow가 배타 모드로 열고 닫으므로,
    // "그 창이 열렸는가"로는 구분되지 않는다(구분이 필요하면 탭까지 봐야 한다).
    // 통합 전 타입(UI_DragonInventoryWindow·UI_DragonSkillWindow)을 그대로 두면 어느 씬에서도 일치하지 않아
    // 열기 단계는 영영 통과하지 못하고 닫기 단계는 진입 즉시 통과한다 - 실제로 그래서 갇혔다.
    public static bool MatchesMode(MonoBehaviour mode, TutorialExclusiveModeKind kind)
    {
        switch (kind)
        {
            case TutorialExclusiveModeKind.BuildMode: return mode is UI_BuildModeWindow;
            case TutorialExclusiveModeKind.WorkerMode: return mode is WorkerModeController;
            case TutorialExclusiveModeKind.Conquest: return mode is ConquestModeController;
            case TutorialExclusiveModeKind.Research: return mode is UI_ResearchWindow;
            case TutorialExclusiveModeKind.BabyDragonInventory: return mode is UI_DragonWindow;
            case TutorialExclusiveModeKind.DragonSkill: return mode is UI_DragonWindow;
            default: return false;
        }
    }

    /// <summary>
    /// 성은 게임 시작 시 RegisterFootprint로 같은 이벤트를 발행하므로(GridMap의 사전 배치 경로),
    /// 종류를 반드시 확인해야 시작 즉시 오발화하지 않는다.
    /// </summary>
    /// <param name="factoryData">생산시설을 종류까지 좁힐 때만 넘긴다. null이면 아무 생산시설이나 통과한다.</param>
    public static bool MatchesBuilding(
        Building building, TutorialBuildingKind kind, ResourceProductionData factoryData)
    {
        switch (kind)
        {
            case TutorialBuildingKind.AnyTower:
                return building is Tower && !(building is BabyDragonTower);

            case TutorialBuildingKind.BabyDragonTower:
                return building is BabyDragonTower;

            case TutorialBuildingKind.ResearchLab:
                return building is ResearchLab;

            case TutorialBuildingKind.Factory:
                // 종류를 지정하지 않았으면 아무 생산시설이나 통과시킨다.
                // 슬라임 농장도 Factory이므로 여기서 함께 통과한다 - 슬라임만 세려면 아래 SlimeFactory를 쓴다.
                return building is Factory factory &&
                       (factoryData == null || factory.Data == factoryData);

            case TutorialBuildingKind.SlimeFactory:
                return building is SlimeFactory;

            case TutorialBuildingKind.Castle:
                return building is Castle;

            case TutorialBuildingKind.SealStone:
                return building is SealStone;

            default:
                return false;
        }
    }
}
