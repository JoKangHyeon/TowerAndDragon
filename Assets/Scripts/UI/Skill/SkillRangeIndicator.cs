using UnityEngine;

/// <summary>GroundPoint 스킬 시전 중 커서 위치에 실제 피해 판정 범위와 동일한 타원 외곽선을
/// 그려 보여준다. LineRenderer로 월드 공간에 타원을 그리며, 반지름이 바뀔 때만 정점을 다시 만든다.</summary>
[RequireComponent(typeof(LineRenderer))]
public class SkillRangeIndicator : MonoBehaviour
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

    /// <summary>지정한 X/Y 반지름으로 타원 표시를 켠다. 스킬 타겟팅 시작 시 호출한다.
    /// 실제 피해 판정(AreaCurrentHealthDamageSkill)과 동일한 반지름을 넘겨야 시각적으로 일치한다.</summary>
    public void Show(float radiusX, float radiusY)
    {
        gameObject.SetActive(true);
        RebuildEllipseIfNeeded(radiusX, radiusY);
    }

    /// <summary>표시를 끈다. 시전 확정/취소 시 호출한다.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>표시 중심을 월드 좌표로 옮긴다. 타겟팅 중 매 프레임 커서 위치로 호출한다.</summary>
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
