using System.Collections.Generic;
using UnityEngine;

// 원정 중인 청크마다 시계 아이콘과 주둔지 고스트를 배치한다. ConquestManager.OnExpeditionsChanged가
// 발생할 때마다 활성 원정 전체를 다시 그린다 - ConqueredChunkBorderRenderer와 동일한 패턴(ComponentPool + 전체 재계산).
[RequireComponent(typeof(GridMap))]
public class ExpeditionMarkerRenderer : MonoBehaviour
{
    [SerializeField]
    private ConquestManager _conquestManager;

    [Tooltip("원정 중 청크 중심에 띄울 시계 아이콘 프리팹.")]
    [SerializeField]
    private SpriteRenderer _clockIconPrefab;

    [SerializeField]
    private float _clockIconYOffset = 1f;

    [Tooltip("완료 시 주둔지가 지어질 위치에 미리 보여줄 반투명 스프라이트 프리팹.")]
    [SerializeField]
    private SpriteRenderer _garrisonGhostPrefab;

    [Range(0f, 1f)]
    [SerializeField]
    private float _garrisonGhostAlpha = 0.5f;

    private GridMap _gridMap;
    private ComponentPool<SpriteRenderer> _clockIconPool;
    private ComponentPool<SpriteRenderer> _garrisonGhostPool;

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
        _clockIconPool = new ComponentPool<SpriteRenderer>(_clockIconPrefab, transform);
        _garrisonGhostPool = new ComponentPool<SpriteRenderer>(_garrisonGhostPrefab, transform);
    }

    private void OnEnable()
    {
        _conquestManager.OnExpeditionsChanged.AddListener(Refresh);
        Refresh();
    }

    private void OnDisable()
    {
        _conquestManager.OnExpeditionsChanged.RemoveListener(Refresh);
    }

    private void Refresh()
    {
        IReadOnlyList<ConquestExpedition> expeditions = _conquestManager.ActiveExpeditions;
        Building garrisonPrefab = _conquestManager.GarrisonPrefab;
        Sprite garrisonSprite = ResolveGarrisonSprite(garrisonPrefab);
        int ghostCount = 0;

        for (int i = 0; i < expeditions.Count; i++)
        {
            ConquestExpedition expedition = expeditions[i];

            SpriteRenderer clockIcon = _clockIconPool.Get(i);
            Vector3 chunkCenter = _gridMap.GetChunkCenterWorld(expedition.TargetChunkCoord);
            Vector3 clockPosition = clockIcon.transform.position;
            clockPosition.x = chunkCenter.x;
            clockPosition.y = chunkCenter.y + _clockIconYOffset;
            clockIcon.transform.position = clockPosition;

            if (garrisonSprite == null)
                continue;

            if (!_conquestManager.TryGetGarrisonPreviewCell(expedition.TargetChunkCoord, out Vector3Int cellCoord))
                continue;

            SpriteRenderer ghost = _garrisonGhostPool.Get(ghostCount);
            ghost.sprite = garrisonSprite;

            // GridMap.ConstructBuilding()과 동일한 위치 계산 - 앵커 셀 하나가 아니라
            // footprint 중심 + 프리팹 자체의 로컬 위치 오프셋을 반영해야 실제 배치 위치와 일치한다.
            Vector3 offset = garrisonPrefab.transform.localPosition;
            Vector3 targetWorldPos = _gridMap.GetFootprintCenterWorld(cellCoord, garrisonPrefab.FootprintShape) + offset;
            Vector3 ghostPosition = ghost.transform.position;
            ghostPosition.x = targetWorldPos.x;
            ghostPosition.y = targetWorldPos.y;
            ghost.transform.position = ghostPosition;

            Color color = ghost.color;
            color.a = _garrisonGhostAlpha;
            ghost.color = color;

            ghostCount++;
        }

        _clockIconPool.DeactivateFrom(expeditions.Count);
        _garrisonGhostPool.DeactivateFrom(ghostCount);
    }

    private static Sprite ResolveGarrisonSprite(Building garrisonPrefab)
    {
        if (garrisonPrefab == null)
            return null;

        SpriteRenderer renderer = garrisonPrefab.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer.sprite : null;
    }
}
