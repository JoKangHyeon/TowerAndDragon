using UnityEngine;
using UnityEngine.EventSystems;

// 마우스 휠로 스킬트리를 확대/축소한다. ScrollRect의 드래그 이동과는 분리해
// Content의 localScale만 조정한다 - Content 피벗이 중앙(0.5,0.5)이라
// 중앙 오각형을 기준으로 대칭 확대/축소된다.
public class UI_DragonSkillTreeZoom : MonoBehaviour, IScrollHandler
{
    private const float DEFAULT_ZOOM_STEP = 0.1f;
    private const float DEFAULT_MIN_SCALE = 0.4f;
    private const float DEFAULT_MAX_SCALE = 1.5f;

    [SerializeField] private RectTransform _target;
    [SerializeField] private float _zoomStep = DEFAULT_ZOOM_STEP;
    [SerializeField] private float _minScale = DEFAULT_MIN_SCALE;
    [SerializeField] private float _maxScale = DEFAULT_MAX_SCALE;

    public void OnScroll(PointerEventData eventData)
    {
        if (!WiringGuard.Require(_target, nameof(_target), this))
        {
            return;
        }

        float delta = eventData.scrollDelta.y * _zoomStep;
        float newScale = Mathf.Clamp(_target.localScale.x + delta, _minScale, _maxScale);
        _target.localScale = new Vector3(newScale, newScale, 1f);
    }
}
