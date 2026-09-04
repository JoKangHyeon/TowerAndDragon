// 값 순서는 직렬화된 데이터(TerrainTileMap 엔트리, Portal._terrainType)와 인덱스 기반
// 스프라이트 배열(UI_ConquestWindow, ChunkInfoOverlayRenderer)이 함께 쓰므로,
// 새 항목은 반드시 맨 뒤에만 추가한다.
public enum TerrainType
{
    Grass, // 초원 지대
    Rock, // 암석 지대
    Volcano, // 화산 지대
    Desert, // 사막 지대
    Snow, // 설원 지대
    Default, // 지형 없음 - 터레인 타일맵에 등록되지 않은 타일과 그리드 밖 좌표
    Road, // 몬스터 스폰 경로 등 지형 자원이 없는 통행로
    Water // 물 - 맵 경계 장식이며 어떤 해금으로도 건설할 수 없다
}
