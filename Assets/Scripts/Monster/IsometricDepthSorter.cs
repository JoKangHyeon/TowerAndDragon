using UnityEngine;

// 몬스터 등 지형 위를 돌아다니는 오브젝트를, 자기가 서 있는 행(고저차를 뺀 평면 좌표 기준)에 맞춰
// 매 프레임 sortingOrder를 갱신한다. 건물은 GridMap이 배치/이동 시 한 번만 정렬하면 되지만, 이
// 오브젝트는 스스로 매 프레임 셀을 옮겨 다니므로 FogTintReceiver와 같은 방식으로 계속 확인한다.
public class IsometricDepthSorter : MonoBehaviour
{
    private SpriteRenderer[] _renderers;
    private MonsterMovement _movement;
    private int _lastAppliedOrder;
    private bool _hasAppliedOrder;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _movement = GetComponent<MonsterMovement>();
    }

    private void Update()
    {
        float groundPlaneWorldY = _movement != null ? _movement.GroundPlanePosition.y : transform.position.y;
        int order = IsometricMath.ComputeDepthSortOrder(groundPlaneWorldY);

        if (_hasAppliedOrder && order == _lastAppliedOrder)
            return;

        _hasAppliedOrder = true;
        _lastAppliedOrder = order;

        foreach (SpriteRenderer spriteRenderer in _renderers)
            spriteRenderer.sortingOrder = order;
    }
}
