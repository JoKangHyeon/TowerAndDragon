using UnityEngine;

/// <summary>월드 공간의 한 지점을 중심으로 원/타원 외곽선을 그려 범위를 표시한다
/// (예: 타워 공격 사거리, 스킬 판정 범위). LineRenderer로 그리며, 반지름이 바뀔 때만 정점을 다시 만든다.</summary>
[RequireComponent(typeof(LineRenderer))]
public class RangeIndicator : MonoBehaviour
{
    private const float CIRCLE_TOTAL_ANGLE_RADIANS = Mathf.PI * 2f;

    [Tooltip("타원을 근사할 선분 개수 - 클수록 부드럽지만 정점이 늘어난다.")]
    [SerializeField] private int _segmentCount = 64;

    private LineRenderer _lineRenderer;
    private float _currentRadiusX = -1f;
    private float _currentRadiusY = -1f;

    // 씬 오브젝트는 비활성 상태로 배치해 둔다 - Show()/Hide()가 활성 여부를 토글하므로
    // 여기서 SetActive(false)를 호출하면 Show()의 첫 활성화 도중 Awake가 실행되며
    // 곧바로 다시 비활성화되어 버린다.
    private void Awake()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.loop = true;
        _lineRenderer.useWorldSpace = false;
    }

    /// <summary>지정한 X/Y 반지름으로 표시를 켠다. 실제 판정 반경(공격 사거리, 스킬 판정 등)과
    /// 동일한 값을 넘겨야 시각적으로 일치한다. 원으로 쓰려면 radiusX와 radiusY를 같은 값으로 넘긴다.</summary>
    public void Show(float radiusX, float radiusY)
    {
        gameObject.SetActive(true);
        RebuildEllipseIfNeeded(radiusX, radiusY);
    }

    /// <summary>표시를 끈다.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>표시 중심을 월드 좌표로 옮긴다.</summary>
    public void SetCenter(Vector3 worldPoint)
    {
        transform.position = worldPoint;
    }

    private void RebuildEllipseIfNeeded(float radiusX, float radiusY)
    {
        if (Mathf.Approximately(radiusX, _currentRadiusX) && Mathf.Approximately(radiusY, _currentRadiusY))
            return;

        _currentRadiusX = radiusX;
        _currentRadiusY = radiusY;

        _lineRenderer.positionCount = _segmentCount;

        for (int i = 0; i < _segmentCount; i++)
        {
            float angle = CIRCLE_TOTAL_ANGLE_RADIANS * i / _segmentCount;
            Vector3 point = new Vector3(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY, 0f);
            _lineRenderer.SetPosition(i, point);
        }
    }
}
