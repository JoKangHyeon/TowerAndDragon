using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 스플라인 경로를 LineRenderer로 그려 게임 뷰/런타임에서도 보이게 하는 시각화 스크립트.
/// SplineContainer가 있는 오브젝트에 붙이면 스플라인을 샘플링해 라인을 갱신한다.
/// 에디터에서도 바로 보이도록 ExecuteAlways로 동작한다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(SplineContainer))]
public class SplinePathVisualizer : MonoBehaviour
{
    private const int SAMPLE_COUNT_DEFAULT = 50;
    private const int SAMPLE_COUNT_MIN = 2;

    [SerializeField] private int _sampleCount = SAMPLE_COUNT_DEFAULT;

    private LineRenderer _lineRenderer;
    private SplineContainer _splineContainer;

    private void OnEnable()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _splineContainer = GetComponent<SplineContainer>();
        Rebuild();
    }

    private void OnValidate()
    {
        if (_sampleCount < SAMPLE_COUNT_MIN)
        {
            _sampleCount = SAMPLE_COUNT_MIN;
        }

        if (_lineRenderer != null && _splineContainer != null)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        bool isClosed = _splineContainer.Spline != null && _splineContainer.Spline.Closed;

        _lineRenderer.useWorldSpace = true;
        _lineRenderer.loop = isClosed;
        _lineRenderer.positionCount = _sampleCount;

        int lastIndex = _sampleCount - 1;
        for (int i = 0; i < _sampleCount; i++)
        {
            float t = (float)i / lastIndex;
            _lineRenderer.SetPosition(i, _splineContainer.EvaluatePosition(t));
        }
    }
}
