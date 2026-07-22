using System.Collections.Generic;
using UnityEngine;

// 원정 중인 청크마다 ChunkClaimTimer(고정 아이콘 + 남은 일수)를 청크 중심에 배치한다.
// ConquestManager.OnExpeditionsChanged가 발생할 때마다 활성 원정 전체를 다시 그린다
// - ConqueredChunkBorderRenderer와 동일한 패턴(ComponentPool + 전체 재계산).
[RequireComponent(typeof(GridMap))]
public class ExpeditionMarkerRenderer : MonoBehaviour
{
    [SerializeField]
    private ConquestManager _conquestManager;

    [Tooltip("원정 중 청크 중심에 띄울 타이머 프리팹(고정 아이콘 + 남은 일수).")]
    [SerializeField]
    private ChunkClaimTimer _timerPrefab;

    [SerializeField]
    private float _timerYOffset = 1f;

    private GridMap _gridMap;
    private ComponentPool<ChunkClaimTimer> _timerPool;

    private void Awake()
    {
        _gridMap = GetComponent<GridMap>();
        _timerPool = new ComponentPool<ChunkClaimTimer>(_timerPrefab, transform);
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

        for (int i = 0; i < expeditions.Count; i++)
        {
            ConquestExpedition expedition = expeditions[i];

            ChunkClaimTimer timer = _timerPool.Get(i);
            Vector3 chunkCenter = _gridMap.GetChunkCenterWorld(expedition.TargetChunkCoord);
            Vector3 position = timer.transform.position;
            position.x = chunkCenter.x;
            position.y = chunkCenter.y + _timerYOffset;
            timer.transform.position = position;

            timer.SetRemainingDays(expedition.DaysRequired - expedition.DaysProgressed);
        }

        _timerPool.DeactivateFrom(expeditions.Count);
    }
}
