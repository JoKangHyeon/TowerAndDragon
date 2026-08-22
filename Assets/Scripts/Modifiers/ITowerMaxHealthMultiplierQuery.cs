// 타워 최대체력 배율 조회 계약.
//
// 공격력·사거리·공속(ITowerStatMultiplierQuery)과 분리한 이유: 그 셋은 TowerAttack이 매 공격마다
// pull하지만 최대체력은 Health가 최댓값을 "값"으로 들고 있어 확정 시점이 필요하다.
// 그래서 소비자가 TowerAttack이 아니라 TowerMaxHealthApplier(밤 시작 시점 push)다.
public interface ITowerMaxHealthMultiplierQuery
{
    float GetMaxHealthMultiplier(TowerData towerData);
}
