using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
// --- 자원 노드 디버깅용 (에디터 전용, 빌드 미포함) ---
// GridMap과 같은 오브젝트에 이 컴포넌트를 붙이면 Play 모드 진입 즉시 각 셀의 AvailableResourceNodes를
// 색깔 오버레이로 게임 뷰에 표시한다(ChunkDebugger와 동일한 스프라이트 풀링 패턴).
// 셀 하나가 자원을 여러 개 동시에 가질 수 있어서, 가진 자원들의 고정 색을 평균 블렌드해 조합마다 구분한다.
// 정확한 값이 필요하면 Play 모드에서 셀을 좌클릭 - 콘솔에 로그로 남는다.
[RequireComponent(typeof(GridMap))]
public class ResourceNodeDebugger : MonoBehaviour
{
    [SerializeField]
    private bool _showResourceNodeGizmos = true;

    [SerializeField]
    private MouseSelectController _mouseSelectController;

    [SerializeField]
    private SpriteRenderer _resourceNodeOverlaySpritePrefab;

    private GridMap _gridMap;
    private ComponentPool<SpriteRenderer> _resourceNodeOverlayPool;

    private const float RESOURCE_NODE_OVERLAY_ALPHA = 0.8f;

    [Header("디버그 - 마우스로 선택한 셀의 자원 노드")]
    [SerializeField]
    private Vector3Int _debugSelectedCellCoord;

    [SerializeField]
    private ResourceType _debugSelectedResourceNodes;

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
        _resourceNodeOverlayPool = new ComponentPool<SpriteRenderer>(_resourceNodeOverlaySpritePrefab, transform);
    }

    // GridMap.Awake()가 자원 노드 적용을 끝낸 뒤(모든 Awake가 Start보다 먼저 실행됨)에 오버레이를 초기화
    private void Start()
    {
        RefreshResourceNodeOverlay();
    }

    private void Update()
    {
        if (!Application.isPlaying || _mouseSelectController == null)
            return;

        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Vector3Int coord = _mouseSelectController.GetHoveredCell();
        ResourceType resourceNodes = _gridMap.GetAvailableResourceNodes(coord);

        _debugSelectedCellCoord = coord;
        _debugSelectedResourceNodes = resourceNodes;

        Debug.Log($"[ResourceNodeDebugger] 선택 셀 {coord} - 자원 노드: {resourceNodes}");
    }

    // 전체 셀을 순회하며 자원 노드가 있는 셀마다 색칠된 오버레이를 깐다 - Play 모드 진입 즉시 게임 뷰에서 바로 보인다.
    private void RefreshResourceNodeOverlay()
    {
        if (_resourceNodeOverlaySpritePrefab == null || _gridMap == null)
            return;

        float yOffset = MouseSelectController.GetYOffsetOrZero(_mouseSelectController);
        int index = 0;

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            foreach (GridCell cell in chunk.Cells)
            {
                if (cell.AvailableResourceNodes == ResourceType.None)
                    continue;

                SpriteRenderer overlay = _resourceNodeOverlayPool.Get(index);
                overlay.gameObject.SetActive(_showResourceNodeGizmos);

                Vector3 worldPos = _gridMap.ConvertGridToWorld(cell.Coord);
                worldPos.y += yOffset;
                overlay.transform.position = worldPos;
                overlay.color = GetResourceNodeColor(cell.AvailableResourceNodes);
                index++;
            }
        }

        _resourceNodeOverlayPool.DeactivateFrom(index);
    }

    // 초원 기본 조합(Food|Wood|Stone 전부) - 가장 흔한 조합이라 초록 지형 위에서 확실히 튀도록 검정으로 고정.
    private const ResourceType GRASS_BASIC_COMBO = ResourceType.Food | ResourceType.Wood | ResourceType.Stone;

    // 플래그별 고정 색을 평균 블렌드 - 여러 자원을 동시에 가진 셀도 조합마다 다른 색으로 구분 가능.
    // 실제 지형 색(초원=초록, 암석=회색, 용암=적/주황, 사막=황토색, 설원=흰색)과 안 겹치면서
    // 서로도 확실히 구분되도록 채도 높은 원색 7개로만 골랐다.
    // 색 범례: Food=빨강 · Wood=주황 · Stone=노랑 · FlameHeart=시안 · SnowCrystal=파랑 · TimeSand=보라 · PhilosopherStone=마젠타
    // 단, Food|Wood|Stone이 전부 있는 셀(초원 기본값)은 검정으로 고정 표시한다.
    private static Color GetResourceNodeColor(ResourceType flags)
    {
        if ((flags & GRASS_BASIC_COMBO) == GRASS_BASIC_COMBO)
            return new Color(0f, 0f, 0f, RESOURCE_NODE_OVERLAY_ALPHA);

        Color sum = Color.clear;
        int count = 0;

        void Add(ResourceType flag, Color color)
        {
            if ((flags & flag) == 0)
                return;

            sum += color;
            count++;
        }

        Add(ResourceType.Food, Color.red);
        Add(ResourceType.Wood, new Color(1f, 0.5f, 0f));
        Add(ResourceType.Stone, new Color(1f, 0.9f, 0f));
        Add(ResourceType.FlameHeart, Color.cyan);
        Add(ResourceType.SnowCrystal, new Color(0f, 0.3f, 1f));
        Add(ResourceType.TimeSand, new Color(0.5f, 0f, 1f));
        Add(ResourceType.PhilosopherStone, new Color(1f, 0f, 0.6f));

        if (count == 0)
            return Color.clear;

        Color blended = sum / count;
        blended.a = RESOURCE_NODE_OVERLAY_ALPHA;
        return blended;
    }
}
#endif
