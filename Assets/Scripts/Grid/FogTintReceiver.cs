using UnityEngine;

// 몬스터 등 지형 위를 돌아다니는 오브젝트를, 자기가 서 있는 셀의 ChunkState에 맞춰 FogOfWarRenderer와
// 같은 밝기로 어둡게 틴트한다. 지형은 셀 상태가 바뀔 때만 GridMap.OnCellChanged로 갱신하면 되지만,
// 이 오브젝트는 스스로 매 프레임 셀을 옮겨 다니므로(스플라인/직선 이동), 이벤트 대신 매 프레임
// 자기 위치의 셀 상태를 직접 조회해 바뀐 경우에만 다시 칠한다.
public class FogTintReceiver : MonoBehaviour
{
    private GridMap _gridMap;
    private FogOfWarRenderer _fogOfWarRenderer;
    private SpriteRenderer[] _renderers;
    private ChunkState _lastAppliedState;
    private bool _hasAppliedState;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _gridMap = FindFirstObjectByType<GridMap>();

        // FogOfWarRenderer가 없는 씬(다른 팀원 테스트 씬 등)에서는 조용히 틴트를 비활성화한다.
        if (_gridMap != null)
            _fogOfWarRenderer = _gridMap.GetComponent<FogOfWarRenderer>();
    }

    private void Update()
    {
        if (_gridMap == null || _fogOfWarRenderer == null)
            return;

        Vector3Int coord = _gridMap.ConvertWorldToGrid(transform.position);
        ChunkState state = _gridMap.GetCellState(coord);

        if (_hasAppliedState && state == _lastAppliedState)
            return;

        _hasAppliedState = true;
        _lastAppliedState = state;
        ApplyTint(state);
    }

    private void ApplyTint(ChunkState state)
    {
        Color tint = _fogOfWarRenderer.GetTintColor(state);

        foreach (SpriteRenderer spriteRenderer in _renderers)
        {
            spriteRenderer.color = tint;
        }
    }
}
