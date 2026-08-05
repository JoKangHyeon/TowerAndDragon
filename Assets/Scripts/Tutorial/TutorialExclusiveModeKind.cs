/// <summary>
/// ExclusiveModeOpened 조건이 기다릴 배타 모드. 씬 참조 대신 종류만 지정하고,
/// TutorialRunner가 열린 모드의 실제 타입과 맞춰본다 - 그래서 배선이 필요 없다.
/// </summary>
public enum TutorialExclusiveModeKind
{
    // 지정하지 않으면 아무 모드도 맞지 않는다(조용히 통과하는 것보다 멈춰서 드러나는 편이 낫다).
    None = 0,

    BuildMode,
    WorkerMode,
    Conquest,
    Research,
    BabyDragonInventory,
    DragonSkill,
}
