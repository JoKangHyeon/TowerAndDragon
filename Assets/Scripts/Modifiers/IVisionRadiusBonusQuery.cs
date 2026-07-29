// 시야 반경 보너스 조회 계약. 기존에는 CastleVisionCoordinator가 ResearchManager를 직접 읽었으나,
// 용 스킬트리(불 새끼용 지역 효과)도 같은 지점에 기여해야 해 인터페이스로 분리한다.
public interface IVisionRadiusBonusQuery
{
    int GetVisionRadiusBonus();
}
