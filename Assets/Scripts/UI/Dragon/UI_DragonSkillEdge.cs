using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 스킬트리 노드 두 개를 잇는 연결선 1개의 시각 표현. UI_DragonSkillNode와 같은 역할 분담이다.
//  - SetEndpoints: 두 점을 잇는 배치(위치·회전·길이). 빌드 때 한 번만 부른다.
//  - Bind: 해금 상태에 따라 바뀌는 것(바탕색·채움).
//
// 선은 "어두운 바탕(_background) + 그 위에 채워지는 앞면(_fill)" 두 장으로 되어 있다.
// 슬라이더 컴포넌트를 쓰지 않는 이유: 상호작용·핸들·값 클램프가 전부 불필요하다.
//
// 채우는 방법은 Image.fillAmount가 아니라 앞면의 anchorMax.y다 - Filled 타입은
// activeSprite가 null이면 통째로 무시되고(uGUI Image.OnPopulateMesh), 이 선은 색만 칠하는
// 스프라이트 없는 Image이기 때문이다. 앵커로 늘리면 스프라이트를 끌어올 필요가 없다.
//
// 채움 방향은 선의 pivot이 하단(0.5, 0)이라는 점에서 나온다 - 로컬 +Y가 곧
// "선행 노드 → 이 노드" 방향이므로, 아래에서 위로 늘리는 것이 그대로 그 방향이다.
public class UI_DragonSkillEdge : MonoBehaviour
{
    private const float FILL_DURATION = 0.4f;
    private const float EMPTY_FILL = 0f;
    private const float FULL_FILL = 1f;

    [Tooltip("아직 못 산 구간을 포함해 항상 보이는 어두운 바탕.")]
    [SerializeField] private Image _background;

    [Tooltip("해금하면 선행 노드 쪽에서 이 노드 쪽으로 채워지는 앞면. " +
        "앵커는 하단 기준으로 늘어나야 한다(anchorMin (0,0), pivot (0.5,0), 오프셋 0).")]
    [SerializeField] private Image _fill;

    private Tween _fillTween;

    // 직전 Bind에서의 점등 상태. 첫 Bind(null)에서는 애니메이션 없이 즉시 반영한다 -
    // 창을 다시 열 때마다 이미 해금된 선이 전부 다시 채워지면 시선이 산만해진다.
    private bool? _wasLit;

    // 노드 하나를 사면 RefreshAll이 연달아 여러 번 돈다(NodeUnlocked · 자원 차감으로 인한
    // ResourceChanged · 상세 패널의 onChanged). 그 두 번째 Bind는 전이가 아니므로,
    // 가드가 없으면 방금 시작한 채움을 죽이고 즉시 꽉 채워 버린다.
    private bool IsFillTweenPlaying => _fillTween != null && _fillTween.IsActive();

    /// <summary>
    /// <paramref name="from"/>(선행 노드 쪽)에서 <paramref name="to"/>(이 선이 켜고 끄는 노드 쪽)로
    /// 선을 놓는다. 좌표는 트리 Content의 anchoredPosition 기준이다.
    /// </summary>
    public void SetEndpoints(Vector2 from, Vector2 to)
    {
        var rect = (RectTransform)transform;
        rect.anchoredPosition = from;

        Vector2 delta = to - from;
        float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
        rect.localRotation = Quaternion.Euler(0f, 0f, -angle);

        Vector2 size = rect.sizeDelta;
        size.y = delta.magnitude;
        rect.sizeDelta = size;
    }

    public void Bind(bool lit, Color attributeColor, Color lockedColor)
    {
        // 바탕은 아직 못 산 선의 색을 그대로 유지한다 - 채움이 걷혀도 갈래가 어느 속성인지 남는다.
        if (_background != null)
        {
            _background.color = DragonSkillNodePalette.EdgeColor(false, attributeColor, lockedColor);
        }

        if (_fill == null)
        {
            _wasLit = lit;
            return;
        }

        _fill.color = DragonSkillNodePalette.EdgeColor(true, attributeColor, lockedColor);

        RectTransform fillRect = _fill.rectTransform;
        var target = new Vector2(fillRect.anchorMax.x, lit ? FULL_FILL : EMPTY_FILL);
        bool isTransition = _wasLit.HasValue && _wasLit.Value != lit;

        if (isTransition)
        {
            _fillTween?.Kill();
            _fillTween = fillRect
                .DOAnchorMax(target, FILL_DURATION)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }
        else if (!IsFillTweenPlaying)
        {
            fillRect.anchorMax = target;
        }

        _wasLit = lit;
    }
}
