using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// 연구 트리 노드 두 개를 잇는 연결선 1개. UI_DragonSkillEdge와 같은 역할 분담이다.
//  - SetCurve: 두 노드를 잇는 곡선 배치. 빌드 때 한 번만 부른다.
//  - Bind: 완료 상태에 따라 바뀌는 것(바탕색·채움).
//
// 선은 "항상 보이는 어두운 바탕 + 그 위로 차오르는 앞면" 두 겹이다. 둘 다 실선이고,
// <b>바탕색은 상태와 무관하게 고정</b>이다 - 상태에 따라 바탕색까지 바꾸면 그 변화가 즉시 튀어
// 선이 차오르는 것보다 먼저 눈에 들어온다(UI_DragonSkillEdge와 같은 규칙).
// 스킬트리 연결선은 pivot을 끝에 두고 앵커를 늘려 채우지만, 이쪽은 베지어 곡선이라
// 그 방법을 쓸 수 없다 - 대신 UI_CurvedEdge.Progress로 리본 메시를 호 길이만큼 잘라 낸다.
//
// 채움 방향은 곡선 점 순서에서 나온다 - 점은 선행 노드에서 이 선이 켜는 노드 쪽으로 만들므로,
// 앞에서부터 그리는 것이 그대로 "선행 → 다음" 방향이다.
//
// 겹은 프리팹이 아니라 코드로 만든다. 트리 자체가 런타임 생성이라 관례가 같고,
// 두 겹이 같은 좌표계(부모 로컬 = Content 좌표)를 공유해야 해 배선 실수를 만들 여지를 줄인다.
[RequireComponent(typeof(UI_CurvedEdge))]
public sealed class UI_ResearchEdge : MonoBehaviour
{
    private const float FILL_DURATION = 0.4f;
    private const float EMPTY_FILL = 0f;
    private const float FULL_FILL = 1f;

    // 이미 찬 선의 색만 바뀔 때(선행 완료 → 대상 완료) 쓰는 시간. 채우는 것보다 짧게 잡아
    // "차오른다"와 "물든다"가 서로 다른 동작으로 읽히게 한다.
    private const float COLOR_DURATION = 0.25f;

    private UI_CurvedEdge _background;
    private UI_CurvedEdge _fill;
    private Tween _fillTween;
    private Tween _colorTween;

    // 직전 Bind에서 선이 차 있었는지. 첫 Bind(null)에서는 애니메이션 없이 즉시 반영한다 -
    // 창을 다시 열 때마다 이미 찬 선이 전부 다시 차오르면 시선이 산만해진다.
    private bool? _wasFilled;

    // 연구 하나를 끝내면 RefreshAll이 연달아 여러 번 돈다(NodeCompleted · 자원 차감으로 인한
    // ResourceChanged · RP 변경 · 상세 패널의 onChanged). 그 두 번째 Bind는 전이가 아니므로,
    // 가드가 없으면 방금 시작한 채움을 죽이고 즉시 꽉 채워 버린다.
    private bool IsFillTweenPlaying => _fillTween != null && _fillTween.IsActive();

    /// <summary>
    /// 곡선을 놓는다. 점은 선행 노드 → 이 선이 켜는 노드 순서이고, 좌표는 부모(트리 Content) 기준이다.
    /// 두 겹이 같은 곡선을 쓴다 - 앞면이 바탕을 정확히 덮으며 자라야 한다.
    /// </summary>
    public void SetCurve(IReadOnlyList<Vector2> points, float thickness)
    {
        EnsureLayers();

        _background.SetCurve(points, thickness);
        _fill.SetCurve(points, thickness);
    }

    /// <summary>
    /// <paramref name="filled"/>는 "선이 차 있어야 하는가"다 - 선행만 끝난 선도 차오른다.
    /// 완료한 선과의 구분은 <paramref name="fillColor"/>의 밝기가 맡는다.
    /// </summary>
    public void Bind(bool filled, Color fillColor, Color backgroundColor)
    {
        EnsureLayers();

        // 바탕은 상태와 무관하게 늘 같은 색이다 - 완료해도 남아 선행 관계가 사라지지 않는다.
        _background.color = backgroundColor;
        ApplyFillColor(fillColor);

        float target = filled ? FULL_FILL : EMPTY_FILL;
        bool isTransition = _wasFilled.HasValue && _wasFilled.Value != filled;

        if (isTransition)
        {
            _fillTween?.Kill();
            _fillTween = DOTween
                .To(() => _fill.Progress, progress => _fill.Progress = progress, target, FILL_DURATION)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }
        else if (!IsFillTweenPlaying)
        {
            _fill.Progress = target;
        }

        _wasFilled = filled;
    }

    // 이미 차 있는 선의 색이 바뀌는 경우(선행 완료 → 대상 완료)에는 색을 흘려 넣는다.
    // 그냥 대입하면 그 순간이 툭 튀어서, 정작 보여주려던 "차오름"보다 먼저 눈에 들어온다.
    private void ApplyFillColor(Color fillColor)
    {
        if (_fill.color == fillColor)
        {
            return;
        }

        _colorTween?.Kill();

        // 아직 안 보이는 선은 흘릴 이유가 없다 - 차오르기 시작할 때 이미 제 색이어야 한다.
        if (!_wasFilled.HasValue || _fill.Progress <= EMPTY_FILL)
        {
            _fill.color = fillColor;
            return;
        }

        _colorTween = _fill.DOColor(fillColor, COLOR_DURATION).SetLink(gameObject);
    }

    private void EnsureLayers()
    {
        if (_fill != null)
        {
            return;
        }

        _background = GetComponent<UI_CurvedEdge>();
        _background.raycastTarget = false;

        // 자식은 부모보다 위에 그려진다 - 앞면을 자식으로 두면 바탕을 그대로 덮는다.
        var fillObject = new GameObject(
            "EdgeFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(UI_CurvedEdge));
        var rect = (RectTransform)fillObject.transform;
        rect.SetParent(transform, false);

        // 점을 부모 좌표 그대로 넘기므로 두 겹의 원점이 같아야 한다.
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        _fill = fillObject.GetComponent<UI_CurvedEdge>();
        _fill.raycastTarget = false;

        // 첫 Bind 전까지는 비워 둔다 - 완료 상태를 모르는 채로 꽉 찬 선이 한 프레임 보이면 안 된다.
        _fill.Progress = EMPTY_FILL;
    }
}
