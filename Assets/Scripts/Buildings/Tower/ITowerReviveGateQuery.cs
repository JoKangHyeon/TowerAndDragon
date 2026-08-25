/// <summary>부활 게이지가 지금 진행해도 되는지 묻는 관문. 구현체가 주입되지 않으면 항상 진행한다
/// (미배선·튜토리얼 씬 = 뮤테이터 없음과 같은 상태).
///
/// Tower가 CycleManager를 직접 들지 않게 하려고 둔 인터페이스다 -
/// <see cref="TowerMorningRestoreSystem"/>의 클래스 주석이 "Tower에 CycleManager 참조를 새로 넣지 않는다"를
/// 규칙으로 정해 두었고, <see cref="ITowerCombatRepairUnlockQuery"/>가 같은 문제를 이미 이 모양으로 풀었다.
///
/// 구현체는 <see cref="TowerMorningRestoreSystem"/>이다. 아침 복구 차단과 낮 동안의 게이지 정지는
/// 같은 규칙(no_morning_restore)의 두 절반이라 한 오브젝트가 함께 소유하는 편이 어긋날 여지가 없다.</summary>
public interface ITowerReviveGateQuery
{
    /// <summary>true면 이번 프레임에 부활 게이지가 오른다. false면 진행만 멈추고 진행도는 보존된다.</summary>
    bool CanAdvanceRevive { get; }
}
