using UnityEngine;

// 지역 페널티를 위치 단위로 증감·무효화하는 외부 훅. 새끼용이 이 인터페이스로 붙는다.
//
// 반환값은 페널티 "크기"에 곱하는 배율이다(감소율이나 유지비 자체에 곱한다):
//   1   = 중립(페널티 원래대로)
//   0   = 완전 무효화
//   0.5 = 절반으로 완화
//   1보다 큰 값 = 페널티 심화
// 배율에 곱하는 게 아니라 페널티 크기에 곱하므로, 완화 소스가 없을 때(중립 1)의 결과가
// 테이블 원본 값과 정확히 같다.
public interface ITerrainPenaltyScaleQuery
{
    // 건물이 아니라 월드 좌표를 받는다 - 완화 판정이 원래 위치(타원 반경)로만 결정되므로,
    // 아직 배치되지 않은 고스트도 배치된 건물과 정확히 같은 경로로 물어볼 수 있다.
    float GetPenaltyScale(Vector3 worldPosition, TerrainType terrain, TerrainPenaltyKind kind);
}
