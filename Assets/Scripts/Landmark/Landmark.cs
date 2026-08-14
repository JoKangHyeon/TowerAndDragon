using UnityEngine;

// 맵에 실제로 존재하는 랜드마크 1개. LandmarkManager가 ChunkLandmarkTable을 보고 생성하며,
// 씬에 미리 배치하지 않는다 - 플레이어가 짓는 건물이 아니라 테이블에서 파생되는 데이터 기반
// 오브젝트다. 그래서 MapStateDto.Buildings의 대상이 아니고(PrefabId가 없다), 세이브는 수령 이력과
// 배치 인구만 LandmarkStateDto에 담는다.
public sealed class Landmark : MonoBehaviour
{
    public LandmarkDataSO Data { get; private set; }
    public Vector2Int ChunkCoord { get; private set; }

    // 상태는 청크에서 파생된다 - 여기에 따로 저장하지 않고 매니저가 갱신해 준다.
    // 점령 여부만이 아니라 상태 전체를 들고 있는 이유: 마커가 안개 아래(Hidden)에서 숨어야 하는데,
    // bool 하나로는 "미점령이지만 보이는" 청크와 "아직 못 본" 청크를 구분할 수 없다.
    public ChunkState State { get; private set; } = ChunkState.Hidden;

    public bool IsConquered => State == ChunkState.Conquered;
    public bool IsRevealed => State != ChunkState.Hidden;

    // 정원이 0인 랜드마크에는 아예 붙지 않는다.
    public LandmarkPopulation Population { get; private set; }

    // 마커를 매니저가 별도 리스트로 들지 않고 랜드마크가 직접 잡고 있는다 - 소멸할 때
    // 매니저 쪽 리스트에서 짝을 찾아 지워야 하는데, Destroy가 프레임 끝까지 지연되어
    // 파괴 직후에는 null 비교로 걸러낼 수 없기 때문이다.
    public LandmarkMarker Marker { get; private set; }

    public bool IsOperating => Population != null && Population.AssignedPopulation > 0;

    public void Initialize(LandmarkDataSO data, Vector2Int chunkCoord)
    {
        Data = data;
        ChunkCoord = chunkCoord;
    }

    public void BindPopulation(LandmarkPopulation population)
    {
        Population = population;
    }

    public void BindMarker(LandmarkMarker marker)
    {
        Marker = marker;
    }

    public void SetState(ChunkState state)
    {
        State = state;
    }
}
