using UnityEngine;

// 몬스터 등 지형 위를 돌아다니는 오브젝트를, 자기가 서 있는 행(고저차를 뺀 평면 좌표 기준)에 맞춰
// 매 프레임 sortingOrder를 갱신한다. 건물은 GridMap이 배치/이동 시 한 번만 정렬하면 되지만, 이
// 오브젝트는 스스로 매 프레임 셀을 옮겨 다니므로 FogTintReceiver와 같은 방식으로 계속 확인한다.
public class IsometricDepthSorter : MonoBehaviour
{
    private SpriteRenderer[] _renderers;
    private MonsterMovement _movement;
    private int _sortingOrderFloor = int.MinValue;
    private int _lastAppliedOrder;
    private bool _hasAppliedOrder;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        _movement = GetComponent<MonsterMovement>();
    }

    // 정렬 순서의 하한. 건물 안에 서는 오브젝트가 그 건물 스프라이트 뒤로 숨지 않게 하는 데 쓴다 -
    // 건물은 앵커 한 칸으로 순서가 정해지므로(GridMap ← IsometricMath.ComputeDepthSortOrder)
    // 앵커보다 뒤쪽 행에 서면 순서가 밀려 묻힌다.
    //
    // 좌표에서 계산한 차이를 오프셋으로 더하는 대신 하한으로 두는 이유: 시각 보정으로 위치를
    // 미세하게 올리거나 내리면 계산한 차이가 어긋나 다시 묻힌다. 하한은 위치가 어떻게 흔들려도
    // "최소 이만큼은 앞" 을 보장한다.
    // 기본값이 int.MinValue라 이 함수를 부르지 않는 몬스터 쪽 동작은 그대로다.
    public void SetSortingOrderFloor(int floor)
    {
        if (_sortingOrderFloor == floor)
            return;

        _sortingOrderFloor = floor;
        _hasAppliedOrder = false; // 다음 Update에서 반드시 다시 반영되도록 적용 기록을 비운다
    }

    private void Update()
    {
        float groundPlaneWorldY = _movement != null ? _movement.GroundPlanePosition.y : transform.position.y;
        int order = Mathf.Max(IsometricMath.ComputeDepthSortOrder(groundPlaneWorldY), _sortingOrderFloor);

        if (_hasAppliedOrder && order == _lastAppliedOrder)
            return;

        _hasAppliedOrder = true;
        _lastAppliedOrder = order;

        foreach (SpriteRenderer spriteRenderer in _renderers)
            spriteRenderer.sortingOrder = order;
    }
}
