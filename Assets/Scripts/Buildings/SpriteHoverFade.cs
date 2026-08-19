using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>마우스를 스프라이트 위에 올리면 반투명하게 전환해 뒤에 가려진 오브젝트가 보이게 한다. (예: 메인 성 뒤의 타워)</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteHoverFade : MonoBehaviour
{
    private const float DEFAULT_HOVERED_ALPHA = 0.4f;
    private const float DEFAULT_FADE_SPEED = 4f;

    [Tooltip("마우스를 올렸을 때 도달할 알파 값.")]
    [SerializeField] private float _hoveredAlpha = DEFAULT_HOVERED_ALPHA;

    [Tooltip("초당 알파 변화량. 클수록 빠르게 전환된다.")]
    [SerializeField] private float _fadeSpeed = DEFAULT_FADE_SPEED;

    private SpriteRenderer _spriteRenderer;
    private Camera _cam;
    private float _originalAlpha;
    private float _currentAlpha;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _cam = Camera.main;
        _originalAlpha = _spriteRenderer.color.a;
        _currentAlpha = _originalAlpha;
    }

    private void Update()
    {
        float targetAlpha = IsMouseOverSprite() ? _hoveredAlpha : _originalAlpha;
        // 호버 페이드는 게임 속도(일시정지/배속)와 무관한 UI 피드백이라 unscaled로 처리한다.
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, _fadeSpeed * Time.unscaledDeltaTime);

        // 하이라이트 등 다른 연출이 RGB를 바꿔도 충돌하지 않도록 알파만 덮어쓴다.
        Color color = _spriteRenderer.color;
        if (Mathf.Approximately(color.a, _currentAlpha))
            return;

        color.a = _currentAlpha;
        _spriteRenderer.color = color;
    }

    private bool IsMouseOverSprite()
    {
        if (Mouse.current == null)
            return false;

        if (_cam == null)
        {
            _cam = Camera.main;

            if (_cam == null)
                return false;
        }

        // 클릭 후보 판정(Building.ContainsWorldPoint)과 같은 함수를 쓴다 -
        // 기준이 갈라지면 "반투명해졌는데 안 눌리는" 영역이 생긴다.
        Vector3 mouseWorld = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

        return SpriteHitTest.Contains(_spriteRenderer, mouseWorld);
    }
}
