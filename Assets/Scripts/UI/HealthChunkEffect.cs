using UnityEngine;

/// <summary>
/// 체력바에서 떨어져 나온 연출용 조각. 제자리에서 위로 조금 떠오르며(ease-out) 서서히 사라진 뒤 스스로 제거된다.
/// DOTween 등 외부 의존성 없이 Update로 직접 움직여, 어떤 환경에서도 컴파일·동작한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class HealthChunkEffect : MonoBehaviour
{
    private RectTransform _rect;
    private CanvasGroup _group;
    private float _lifetime;
    private float _riseDistance;
    private float _startY;
    private float _elapsed;

    /// <summary>생성 직후 스포너가 호출한다. 이 시점부터 상승·페이드를 시작한다.</summary>
    public void Play(float lifetime, float riseDistance)
    {
        _rect = GetComponent<RectTransform>();
        _group = GetComponent<CanvasGroup>();
        _lifetime = lifetime;
        _riseDistance = riseDistance;
        _startY = _rect.anchoredPosition.y;
    }

    private void Update()
    {
        if (_group == null)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        float t = _lifetime > 0 ? Mathf.Clamp01(_elapsed / _lifetime) : 1f;

        // ease-out: 처음엔 빠르게 떠오르다 점점 느려진다.
        float eased = 1 - (1 - t) * (1 - t);
        Vector2 pos = _rect.anchoredPosition;
        pos.y = _startY + _riseDistance * eased;
        _rect.anchoredPosition = pos;

        _group.alpha = 1 - t;

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
