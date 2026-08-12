using UnityEngine;

// 맵에 실제로 존재하는 랜드마크 1개. LandmarkManager가 ChunkLandmarkTable을 보고 생성하며,
// 씬에 미리 배치하지 않는다 - 건물처럼 배치·저장되는 물건이 아니라 테이블에서 파생되는
// 데이터 기반 오브젝트다(MapStateDto.Buildings가 저장되지 않는 이유와 같은 맥락).
public sealed class Landmark : MonoBehaviour
{
    public LandmarkDataSO Data { get; private set; }
    public Vector2Int ChunkCoord { get; private set; }

    // 점령 여부는 청크 상태에서 파생된다 - 여기에 따로 저장하지 않고 매니저가 갱신해 준다.
    public bool IsConquered { get; private set; }

    // 정원이 0인 랜드마크에는 아예 붙지 않는다.
    public LandmarkPopulation Population { get; private set; }

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

    public void SetConquered(bool isConquered)
    {
        IsConquered = isConquered;
    }
}
