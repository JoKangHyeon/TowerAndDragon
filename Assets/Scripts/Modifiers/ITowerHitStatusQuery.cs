// 타워 기본공격이 명중 시 몬스터에 얹을 상태이상(슬로우·화상 등) 조회 계약.
// 기여자가 DragonTreeManager 하나뿐이라 Composite 없이 직접 주입한다.
public interface ITowerHitStatusQuery
{
    StatusEffectSO GetTowerHitStatus(TowerData towerData);
}
