using System.Collections.Generic;
using UnityEngine;

// 점령 모드에서 점령 가능한 청크마다 정보 카드(UI_ChunkInfoCard)를 청크 중심에 하나씩 배치한다.
// 어떤 청크가 점령 가능한지 판정하거나 갱신 시점을 정하지 않는다 - ConquestModeController가
// 점령 가능 청크 목록을 넘겨 Refresh를 호출하고, 모드 종료 시 Clear를 호출한다.
// ExpeditionMarkerRenderer와 동일한 패턴(ComponentPool + 전체 재배치)을 따른다.
public class ChunkInfoOverlayRenderer : MonoBehaviour
{
    // TODO: 스트링테이블 도입 시 _LOC_KEY로 교체(UI_ConquestWindow와 동일 포맷).
    private const string PLUS_VALUE_FORMAT = "+{0}";
    private const string UNLOCKED_RESOURCE_REWARD_LABEL = "++";

    // 보상 표시 순서 - UI_ConquestWindow.REWARD_RESOURCE_TYPES와 동일 순서를 유지한다.
    private static readonly ResourceType[] REWARD_RESOURCE_TYPES =
    {
        ResourceType.Food,
        ResourceType.Wood,
        ResourceType.Stone,
        ResourceType.FlameHeart,
        ResourceType.SnowCrystal,
        ResourceType.TimeSand,
        ResourceType.PhilosopherStone,
    };

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private ConquestManager _conquestManager;

    [Tooltip("자원 아이콘 카탈로그 조회에 사용한다.")]
    [SerializeField]
    private ResourceManager _resourceManager;

    [Tooltip("카드 높이를 다른 오버레이와 맞추기 위한 Y오프셋 기준.")]
    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [Tooltip("청크 위에 띄울 정보 카드 프리팹(월드 스페이스).")]
    [SerializeField]
    private UI_ChunkInfoCard _cardPrefab;

    [Tooltip("지형 스프라이트. 인덱스는 TerrainType 선언 순서와 일치해야 한다(UI_ConquestWindow와 동일).")]
    [SerializeField]
    private Sprite[] _terrainSprites;

    [Tooltip("인구 보상 아이콘. 인구는 ResourceData가 없어 별도 지정한다.")]
    [SerializeField]
    private Sprite _populationIcon;

    private ComponentPool<UI_ChunkInfoCard> _cardPool;
    private readonly List<(Sprite Icon, Color IconColor, string Label)> _rewardBuffer = new();

    private void Awake()
    {
        _cardPool = new ComponentPool<UI_ChunkInfoCard>(_cardPrefab, transform);
    }

    // 점령 가능한 청크 좌표 목록을 받아, 각 청크 중심에 카드를 하나씩 배치한다.
    public void Refresh(IReadOnlyList<Vector2Int> chunkCoords)
    {
        for (int i = 0; i < chunkCoords.Count; i++)
        {
            Vector2Int coord = chunkCoords[i];

            UI_ChunkInfoCard card = _cardPool.Get(i);

            Vector3 position = _gridMap.GetChunkCenterWorld(coord);
            position.y += MouseSelectController.GetYOffsetOrZero(_mouseSelectController);
            card.transform.position = position;

            Sprite terrainSprite = ResolveTerrainSprite(_conquestManager.GetDominantTerrain(coord));
            BuildRewardEntries(coord);
            card.Setup(terrainSprite, _rewardBuffer);
        }

        _cardPool.DeactivateFrom(chunkCoords.Count);
    }

    // 점령 모드가 꺼질 때 호출 - 모든 카드를 숨긴다.
    public void Clear() => _cardPool.DeactivateAll();

    // 이 청크를 점령했을 때 얻는 보상 항목(인구 + 해금 자원)을 재사용 버퍼에 채운다.
    // 버퍼는 카드가 Setup에서 즉시 소비하므로 청크마다 재사용해도 안전하다.
    private void BuildRewardEntries(Vector2Int coord)
    {
        _rewardBuffer.Clear();

        int populationReward = _conquestManager.GetPopulationReward(coord);
        if (populationReward > 0)
        {
            _rewardBuffer.Add((_populationIcon, Color.white, string.Format(PLUS_VALUE_FORMAT, populationReward)));
        }

        ResourceType unlocked = _conquestManager.GetUnlockedResources(coord);
        foreach (ResourceType type in REWARD_RESOURCE_TYPES)
        {
            if ((unlocked & type) == 0)
                continue;

            _rewardBuffer.Add((ResolveResourceIcon(type), DragonAttributePalette.TintFor(type), UNLOCKED_RESOURCE_REWARD_LABEL));
        }
    }

    private Sprite ResolveTerrainSprite(TerrainType terrain)
    {
        int index = (int)terrain;
        return _terrainSprites != null && index >= 0 && index < _terrainSprites.Length ? _terrainSprites[index] : null;
    }

    // 자원 아이콘은 데이터 에셋(ResourceData)이 단일 출처 - 카탈로그에서 종류로 조회한다.
    private Sprite ResolveResourceIcon(ResourceType type)
    {
        if (_resourceManager != null && _resourceManager.Catalog != null &&
            _resourceManager.Catalog.TryGet(type, out ResourceData data))
        {
            return data.Icon;
        }

        return null;
    }
}
