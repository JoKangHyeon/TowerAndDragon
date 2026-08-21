using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 자원 소모 안내 한 줄(아이콘 + 수량). Overlay_Consume 프리팹의 루트에 붙는다.
// 떠오르며 투명해진 뒤 스스로 파괴된다 - 풀링하지 않는 이유는 건설 한 번에 자원 종류 수(보통 1~2개)
// 만큼만 생기고 수명이 1초 미만이기 때문이다.
//
// 자원이 여러 종류일 때 이 프리팹을 여러 개 찍어 세로로 쌓는 일은 UI_ConsumeOverlay가 한다 -
// 이 컴포넌트는 자기 한 줄만 안다.
public class UI_ConsumeOverlayRow : MonoBehaviour
{
    private const float TRANSPARENT = 0f;
    private const float OPAQUE = 1f;

    // 좌우 흔들림의 왕복 횟수와 반동. 진폭(세기)만 호출부가 정하고 결은 여기서 고정한다.
    private const int SWAY_VIBRATO = 6;
    private const float SWAY_ELASTICITY = 1f;

    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _amountText;

    [Tooltip("떠오르며 투명해지는 연출의 대상. 알파를 한 번에 다루려고 루트에 둔다.")]
    [SerializeField] private CanvasGroup _group;

    [Tooltip("좌우로 흔들릴 대상. 루트가 아니라 자식이어야 한다 - 루트의 anchoredPosition은 " +
        "떠오르는 트윈이 쓰고 있어서, 같은 값을 흔들면 서로 덮어쓴다.")]
    [SerializeField] private RectTransform _swayTarget;

    private RectTransform _rect;

    private void Awake()
    {
        _rect = (RectTransform)transform;
    }

    /// <summary>
    /// 내용을 채우고 연출을 재생한다. 연출이 끝나면 이 오브젝트는 파괴된다.
    /// </summary>
    /// <param name="delay">등장까지의 대기 시간. 여러 줄을 순서대로 내보내는 데 쓴다.</param>
    /// <param name="riseDistance">떠오를 거리(캔버스 로컬 단위).</param>
    /// <param name="swayStrength">좌우로 흔들릴 진폭. 0이면 흔들지 않는다.</param>
    public void Play(
        Sprite icon,
        string amountText,
        float delay,
        float riseDistance,
        float duration,
        float swayStrength)
    {
        if (_icon != null)
        {
            // 아이콘이 없는 자원(카탈로그 미등록)이면 빈 사각형이 남지 않게 끈다.
            _icon.sprite = icon;
            _icon.enabled = icon != null;
        }

        if (_amountText != null)
        {
            _amountText.text = amountText;
        }

        // 자기 차례가 오기 전에는 투명하다 - 대기 중에 보이면 "순서대로 나온다"가 아니라
        // "다 떠 있다가 하나씩 움직인다"로 보인다.
        if (_group != null)
        {
            _group.alpha = TRANSPARENT;
        }

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);

        sequence.Insert(delay, _rect
            .DOAnchorPosY(_rect.anchoredPosition.y + riseDistance, duration)
            .SetEase(Ease.OutQuad));

        if (_group != null)
        {
            // From으로 줘야 대기 중 알파가 0으로 유지된다 - 미리 1을 대입하면 순서가 무너진다.
            sequence.Insert(delay, _group
                .DOFade(TRANSPARENT, duration)
                .From(OPAQUE)
                .SetEase(Ease.InQuad));
        }

        if (_swayTarget != null && swayStrength > 0f)
        {
            // 떠오르는 시간 내내 흔들리며 진폭이 잦아든다.
            sequence.Insert(delay, _swayTarget.DOPunchAnchorPos(
                new Vector2(swayStrength, 0f), duration, SWAY_VIBRATO, SWAY_ELASTICITY));
        }

        sequence.OnComplete(() => Destroy(gameObject));
    }
}
