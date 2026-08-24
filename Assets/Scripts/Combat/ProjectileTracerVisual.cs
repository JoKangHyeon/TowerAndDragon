using UnityEngine;

/// <summary>근거리 명중 뒤 잠깐 남는 코어·글로우 이중 트레이서를 그린다.</summary>
public sealed class ProjectileTracerVisual : MonoBehaviour
{
    [SerializeField] private LineRenderer _glowRenderer;
    [SerializeField] private LineRenderer _coreRenderer;

    private float _glowStartWidth;
    private float _glowEndWidth;
    private float _coreStartWidth;
    private float _coreEndWidth;
    private float _durationSeconds;
    private float _elapsedSeconds;
    private float _brightnessMultiplier;
    private Color _coreColor;
    private Color _glowColor;
    private bool _hasCachedRendererWidths;
    private bool _isPlaying;

    private void Awake()
    {
        CacheRendererWidths();
    }

    private void CacheRendererWidths()
    {
        if (_hasCachedRendererWidths)
        {
            return;
        }

        if (_glowRenderer != null)
        {
            _glowStartWidth = _glowRenderer.startWidth;
            _glowEndWidth = _glowRenderer.endWidth;
        }

        if (_coreRenderer != null)
        {
            _coreStartWidth = _coreRenderer.startWidth;
            _coreEndWidth = _coreRenderer.endWidth;
        }

        _hasCachedRendererWidths = true;
    }

    public void Play(
        Vector3 startPosition,
        Vector3 endPosition,
        float durationSeconds,
        float widthMultiplier,
        float brightnessMultiplier,
        Color coreColor,
        Color glowColor)
    {
        CacheRendererWidths();

        _durationSeconds = durationSeconds;
        _elapsedSeconds = 0f;
        _brightnessMultiplier = brightnessMultiplier;
        _coreColor = coreColor;
        _glowColor = glowColor;
        _isPlaying = true;

        ConfigureRenderer(
            _glowRenderer,
            startPosition,
            endPosition,
            _glowStartWidth * widthMultiplier,
            _glowEndWidth * widthMultiplier);
        ConfigureRenderer(
            _coreRenderer,
            startPosition,
            endPosition,
            _coreStartWidth * widthMultiplier,
            _coreEndWidth * widthMultiplier);
        ApplyFade(1f);
    }

    private void Update()
    {
        if (!_isPlaying || _durationSeconds <= 0f)
        {
            return;
        }

        _elapsedSeconds += Time.deltaTime;
        float alpha = 1f - Mathf.Clamp01(_elapsedSeconds / _durationSeconds);
        ApplyFade(alpha);

        if (alpha <= 0f)
        {
            _isPlaying = false;
        }
    }

    private void OnDisable()
    {
        _isPlaying = false;
    }

    private static void ConfigureRenderer(
        LineRenderer lineRenderer,
        Vector3 startPosition,
        Vector3 endPosition,
        float startWidth,
        float endWidth)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
        lineRenderer.startWidth = startWidth;
        lineRenderer.endWidth = endWidth;
        lineRenderer.enabled = true;
    }

    private void ApplyFade(float alpha)
    {
        SetRendererColor(_glowRenderer, _glowColor, alpha, _brightnessMultiplier);
        SetRendererColor(_coreRenderer, _coreColor, alpha, _brightnessMultiplier);
    }

    private static void SetRendererColor(
        LineRenderer lineRenderer,
        Color color,
        float alpha,
        float brightnessMultiplier)
    {
        if (lineRenderer == null)
        {
            return;
        }

        Color resolvedColor = new Color(
            color.r * brightnessMultiplier,
            color.g * brightnessMultiplier,
            color.b * brightnessMultiplier,
            color.a * alpha);

        lineRenderer.startColor = resolvedColor;
        lineRenderer.endColor = new Color(
            resolvedColor.r,
            resolvedColor.g,
            resolvedColor.b,
            0f);
    }
}
