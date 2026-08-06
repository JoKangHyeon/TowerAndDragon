using UnityEngine;
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
// --- 자원 노드 디버깅용 (에디터 전용, 빌드 미포함) ---
// GridMap과 같은 오브젝트에 이 컴포넌트를 붙이면 Play 모드 진입 즉시 각 셀의 AvailableResourceNodes를
// 색깔 오버레이로 게임 뷰에 표시한다(ComponentPool 기반 스프라이트 풀링 패턴).
// 셀 하나가 자원을 여러 개 동시에 가질 수 있어서, 가진 자원들의 고정 색을 평균 블렌드해 조합마다 구분한다.
// 정확한 값(자원 노드 + 기본 생산량)이 필요하면 Play 모드에서 셀을 좌클릭 - 콘솔에 로그로 남는다.
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

    // 생산량이 많을수록 오버레이가 불투명해지도록(알파값 증가) 표시한다 - 최소 알파는 고정, 최대는 1(완전 불투명).
    private const float MIN_OVERLAY_ALPHA = 0.6f;
    private const float MAX_OVERLAY_ALPHA = 1f;

    [Serializable]
    private struct ResourceYieldEntry
    {
        public ResourceType Resource;
        public int Yield;
    }

    [Header("디버그 - 마우스로 선택한 셀의 자원 노드")]
    [SerializeField]
    private Vector3Int _debugSelectedCellCoord;

    [SerializeField]
    private ResourceType _debugSelectedResourceNodes;

    [SerializeField]
    private int _debugSelectedBaseYield;

    [SerializeField]
    private List<ResourceYieldEntry> _debugYieldByResource = new();

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
        int baseYield = _gridMap.GetBaseYield(coord);

        _debugSelectedCellCoord = coord;
        _debugSelectedResourceNodes = resourceNodes;
        _debugSelectedBaseYield = baseYield;

        _debugYieldByResource.Clear();
        var sb = new StringBuilder();
        sb.Append($"[ResourceNodeDebugger] 선택 셀 {coord} - 자원 노드: {resourceNodes}, 기본 생산력: {baseYield}");

        bool first = true;
        foreach (ResourceType flag in EnumerateResourceFlags(resourceNodes))
        {
            int yield = _gridMap.GetYield(coord, flag);
            _debugYieldByResource.Add(new ResourceYieldEntry { Resource = flag, Yield = yield });
            sb.Append(first ? $" | {flag}: {yield}" : $", {flag}: {yield}");
            first = false;
        }

        Debug.Log(sb.ToString());
    }

    private static IEnumerable<ResourceType> EnumerateResourceFlags(ResourceType flags)
    {
        foreach (ResourceType value in (ResourceType[])Enum.GetValues(typeof(ResourceType)))
        {
            if (value != ResourceType.None && (flags & value) != 0)
                yield return value;
        }
    }

    // 전체 셀을 순회하며 자원 노드가 있는 셀마다 색칠된 오버레이를 깐다 - Play 모드 진입 즉시 게임 뷰에서 바로 보인다.
    private void RefreshResourceNodeOverlay()
    {
        if (_resourceNodeOverlaySpritePrefab == null || _gridMap == null)
            return;

        float yOffset = MouseSelectController.GetYOffsetOrZero(_mouseSelectController);

        // 알파값을 생산량 기준으로 정규화하기 위해, 먼저 표시 대상 셀들의 생산량 범위(min~max)를 구해둔다.
        int minYield = int.MaxValue;
        int maxYield = int.MinValue;

        foreach (Chunk chunk in _gridMap.GetAllChunks())
        {
            foreach (GridCell cell in chunk.Cells)
            {
                if (cell.AvailableResourceNodes == ResourceType.None)
                    continue;

                int yield = _gridMap.GetBaseYield(cell.Coord);
                minYield = Mathf.Min(minYield, yield);
                maxYield = Mathf.Max(maxYield, yield);
            }
        }

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

                float alpha = ResolveYieldAlpha(_gridMap.GetBaseYield(cell.Coord), minYield, maxYield);
                overlay.color = GetResourceNodeColor(cell.AvailableResourceNodes, alpha);
                index++;
            }
        }

        _resourceNodeOverlayPool.DeactivateFrom(index);
    }

    // 실제 맵 분포상 셀 대부분이 최소값 근처에 몰려있어서(예: 절반이 최저~최저+1 구간),
    // 선형 정규화만 쓰면 알파 차이가 0.6~0.63 정도로 거의 안 보인다.
    // 지수를 1보다 작게 줘서 저구간 차이를 시각적으로 크게 벌려준다(고구간은 그만큼 압축됨).
    private const float CONTRAST_EXPONENT = 0.5f;

    // 표시 대상 셀들 중 최소~최대 생산량 구간 안에서 0(최소 알파)~1(최대 알파)로 정규화한다.
    // 전부 같은 생산량이면(구간 폭 0) 항상 최대 알파로 표시한다.
    private static float ResolveYieldAlpha(int yield, int minYield, int maxYield)
    {
        if (maxYield <= minYield)
            return MAX_OVERLAY_ALPHA;

        float normalized = (float)(yield - minYield) / (maxYield - minYield);
        float contrasted = Mathf.Pow(normalized, CONTRAST_EXPONENT);
        return Mathf.Lerp(MIN_OVERLAY_ALPHA, MAX_OVERLAY_ALPHA, contrasted);
    }

    // 초원 기본 조합(Food|Wood|Stone 전부) - 가장 흔한 조합이라 초록 지형 위에서 확실히 튀도록 검정으로 고정.
    private const ResourceType GRASS_BASIC_COMBO = ResourceType.Food | ResourceType.Wood | ResourceType.Stone;

    // 플래그별 고정 색을 평균 블렌드 - 여러 자원을 동시에 가진 셀도 조합마다 다른 색으로 구분 가능.
    // 실제 지형 색(초원=초록, 암석=회색, 용암=적/주황, 사막=황토색, 설원=흰색)과 안 겹치면서
    // 서로도 확실히 구분되도록 채도 높은 원색 7개로만 골랐다.
    // 색 범례: Food=빨강 · Wood=주황 · Stone=노랑 · FlameHeart=시안 · SnowCrystal=파랑 · TimeSand=보라 · PhilosopherStone=마젠타
    // 단, Food|Wood|Stone이 전부 있는 셀(초원 기본값)은 검정으로 고정 표시한다.
    private static Color GetResourceNodeColor(ResourceType flags, float alpha)
    {
        if ((flags & GRASS_BASIC_COMBO) == GRASS_BASIC_COMBO)
            return new Color(0f, 0f, 0f, alpha);

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
        blended.a = alpha;
        return blended;
    }
}
#endif
