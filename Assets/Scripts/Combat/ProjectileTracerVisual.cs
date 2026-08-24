using UnityEngine;

/// <summary>
/// 근거리 명중 뒤 잠깐 남는 코어·글로우 이중 트레이서를 그린다.
///
/// <b>몬스터 쪽이 밝고 굵으며, 타워 쪽으로 가면서 투명해진다.</b> v1은 반대였다 —
/// 플레이어 시선이 있는 몬스터 쪽이 가장 흐려서 머즐 섬광 옆의 혹으로만 읽혔다.
/// 방향을 뒤집으면 총픽셀은 그대로인데 몬스터 쪽 밀도가 1.7배가 되고, 무엇보다
/// 낮은 강도에서 몬스터 쪽이 임계 알파 아래로 깎이는 문제가 사라진다(작업노트 5-3-1 ⑤).
///
/// 폭은 프리팹의 <b>비율</b>만 쓰고 절대값은 부르는 쪽이 월드 단위로 준다 -
/// 화면 폭을 줌과 무관하게 유지하기 위해서다(5-1 ④).
/// </summary>
public sealed class ProjectileTracerVisual : MonoBehaviour
{
    [SerializeField] private LineRenderer _glowRenderer;
    [SerializeField] private LineRenderer _coreRenderer;

    // 프리팹에 저작된 폭. Tail은 타워 쪽(가늘다), Head는 몬스터 쪽(굵다).
    // 반전 이후의 프리팹 기준이며, 이 대소가 뒤집히면 그림도 뒤집힌다.
    private float _glowTailWidth;
    private float _glowHeadWidth;
    private float _coreTailWidth;
    private float _coreHeadWidth;

    private float _durationSeconds;
    private float _elapsedSeconds;
    private float _intensity;
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
            _glowTailWidth = _glowRenderer.startWidth;
            _glowHeadWidth = _glowRenderer.endWidth;
        }

        if (_coreRenderer != null)
        {
            _coreTailWidth = _coreRenderer.startWidth;
            _coreHeadWidth = _coreRenderer.endWidth;
        }

        _hasCachedRendererWidths = true;
    }

    /// <summary>
    /// 트레이서를 재생한다. 풀에서 재사용된 인스턴스라도 여기서 폭·색이 전부 다시 써지므로
    /// 지난번 타워의 색이 한 프레임이라도 남지 않는다.
    /// </summary>
    /// <param name="startPosition">타워 쪽. 흐린 꼬리가 된다.</param>
    /// <param name="endPosition">몬스터 쪽. 밝은 머리가 된다.</param>
    /// <param name="headWidthWorld">몬스터 쪽 글로우 폭(월드). 코어는 프리팹 비율로 따라온다.</param>
    /// <param name="intensity">길이 램프 강도. <b>알파에만 걸린다</b>(작업노트 5-3-1 ③).</param>
    public void Play(
        Vector3 startPosition,
        Vector3 endPosition,
        float durationSeconds,
        float headWidthWorld,
        float intensity,
        Color coreColor,
        Color glowColor)
    {
        CacheRendererWidths();

        _durationSeconds = durationSeconds;
        _elapsedSeconds = 0f;
        _intensity = Mathf.Clamp01(intensity);
        _coreColor = coreColor;
        _glowColor = glowColor;
        _isPlaying = true;

        // 글로우와 코어에 같은 배율을 건다 - 둘의 굵기 관계는 프리팹 한 곳에서만 정해진다.
        // 프리팹의 몬스터 쪽 폭이 0이면(저작 사고) 배율을 포기하고 저작값을 그대로 쓴다.
        float widthScale = _glowHeadWidth > 0f ? headWidthWorld / _glowHeadWidth : 1f;

        ConfigureRenderer(
            _glowRenderer,
            startPosition,
            endPosition,
            _glowTailWidth * widthScale,
            _glowHeadWidth * widthScale);
        ConfigureRenderer(
            _coreRenderer,
            startPosition,
            endPosition,
            _coreTailWidth * widthScale,
            _coreHeadWidth * widthScale);
        ApplyFade(1f);
    }

    // 배속과 무관한 실시간으로 페이드한다. 3배속에서 스케일 시간을 쓰면 0.1초가 실제로
    // 2프레임이 되어, 짧게 보이는 것을 보완하려는 연출이 스스로 짧아진다.
    // 반납 타이머(ProjectilePool)도 같은 기준이어야 한다 - 한쪽만 바꾸면 어긋난다.
    private void Update()
    {
        if (!_isPlaying || _durationSeconds <= 0f)
        {
            return;
        }

        _elapsedSeconds += Time.unscaledDeltaTime;
        float fade = 1f - Mathf.Clamp01(_elapsedSeconds / _durationSeconds);
        ApplyFade(fade);

        if (fade <= 0f)
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
        float tailWidth,
        float headWidth)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.positionCount = 2;
        lineRenderer.SetPosition(0, startPosition);
        lineRenderer.SetPosition(1, endPosition);
        lineRenderer.startWidth = tailWidth;
        lineRenderer.endWidth = headWidth;
        lineRenderer.enabled = true;
    }

    private void ApplyFade(float fade)
    {
        float alpha = _intensity * fade;

        SetRendererColor(_glowRenderer, _glowColor, alpha);
        SetRendererColor(_coreRenderer, _coreColor, alpha);
    }

    // 타워 쪽(start)은 투명, 몬스터 쪽(end)이 불투명이다. 색은 건드리지 않는다 -
    // v1의 밝기 배율은 코어가 이미 흰색이고 머티리얼이 가산이 아니라 사실상 무효였고,
    // v2는 그 배율을 아예 쓰지 않는다(강도는 알파로만 표현한다).
    private static void SetRendererColor(LineRenderer lineRenderer, Color color, float alpha)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.startColor = new Color(color.r, color.g, color.b, 0f);
        lineRenderer.endColor = new Color(color.r, color.g, color.b, color.a * alpha);
    }
}
