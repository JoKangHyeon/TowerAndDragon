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
    private MonsterMovement _movement;
    private ChunkState _lastAppliedState;
    private bool _hasAppliedState;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _movement = GetComponent<MonsterMovement>();
        _gridMap = FindFirstObjectByType<GridMap>();

        // FogOfWarRenderer가 없는 씬(다른 팀원 테스트 씬 등)에서는 조용히 틴트를 비활성화한다.
        if (_gridMap != null)
            _fogOfWarRenderer = _gridMap.GetComponent<FogOfWarRenderer>();
    }

    private void Update()
    {
        if (_gridMap == null || _fogOfWarRenderer == null)
            return;

        // 이동 컴포넌트가 있으면 자기가 서 있는 평면 좌표를 알고 있으므로 평면 역변환으로 바로 셀을 얻는다.
        // 이 경우 PickCellAtWorldPoint를 쓰면 안 된다 - 앞쪽에 더 높은 절벽이 있으면 몬스터가 실제로
        // 밟고 있는 셀 대신 그 절벽 셀이 잡힌다.
        // 이동 컴포넌트가 없는 오브젝트는 지형 위에 직접 놓인 것이므로 화면 기준으로 타일을 고른다.
        Vector3Int coord = _movement != null
            ? _gridMap.ConvertWorldToGrid(_movement.GroundPlanePosition)
            : _gridMap.PickCellAtWorldPoint(transform.position);

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
