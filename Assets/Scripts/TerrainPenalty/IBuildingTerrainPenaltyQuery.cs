// 건물 하나가 받는 지역 페널티 조회. Factory/TowerAttack이 TerrainPenaltySystem(씬 오브젝트)을
// 직접 참조하지 않도록 하는 얇은 계약이다 - 배선되지 않은 씬(팀원 테스트 씬 등)에서는 null로
// 남아 페널티 없이 기존 동작을 그대로 유지한다(ITowerStatMultiplierQuery와 같은 관례).
public interface IBuildingTerrainPenaltyQuery
{
    TerrainPenaltyModifiers Resolve(Building building);
}
