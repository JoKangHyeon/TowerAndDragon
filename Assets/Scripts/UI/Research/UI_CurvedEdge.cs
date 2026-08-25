using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 연구 트리의 곡선 연결선 하나. 점 목록을 받아 일정 두께의 리본 메시로 그린다.
//
// uGUI에는 선을 그리는 기본 요소가 없다. Image를 잘게 쪼개 회전시켜 이으면 이음매가 각지고
// 개수도 폭발하므로(선 하나에 십수 개), 곡선 하나를 Graphic 하나로 직접 메시로 만든다.
//
// 점선은 UV로 낸다 - u를 "누적 길이 / 점선 주기"로 깔면 곡률에 상관없이 점 간격이 일정해진다.
// (세그먼트 길이로 UV를 매기면 곡선이 급한 구간에서 점이 뭉친다)
//
// <see cref="Progress"/>로 곡선의 앞부분만 그릴 수 있다. 스킬트리 연결선처럼 "해금하면 선이
// 차오르는" 표현에 쓴다 - 곡선이라 앵커를 늘려서는 만들 수 없고, 메시를 호 길이로 잘라야 한다.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_CurvedEdge : MaskableGraphic
{
    private const float MIN_THICKNESS = 0.5f;
    private const float FULL_PROGRESS = 1f;

    private readonly List<Vector2> _points = new();
    private readonly List<float> _cumulativeLength = new();

    private float _thickness = 4f;
    private float _dashPeriod = 12f;
    private Texture _dashTexture;
    private float _progress = FULL_PROGRESS;

    public override Texture mainTexture => _dashTexture != null ? _dashTexture : base.mainTexture;

    /// <summary>
    /// 곡선을 앞에서부터 얼마나 그릴지(0~1). 호 길이 기준이라 곡률이 변해도 속도가 일정하다.
    /// 0이면 아무것도 그리지 않는다.
    /// </summary>
    public float Progress
    {
        get => _progress;
        set
        {
            float clamped = Mathf.Clamp01(value);

            if (Mathf.Approximately(_progress, clamped))
            {
                return;
            }

            _progress = clamped;
            SetVerticesDirty();
        }
    }

    /// <summary>
    /// 곡선을 다시 잡는다. 점은 이 RectTransform의 로컬 좌표(= 트리 Content 좌표)로 준다.
    /// </summary>
    public void SetCurve(IReadOnlyList<Vector2> points, float thickness, Texture dashTexture, float dashPeriod)
    {
        _points.Clear();
        _cumulativeLength.Clear();

        if (points != null)
        {
            for (int i = 0; i < points.Count; i++)
            {
                // 같은 자리가 연달아 들어오면 접선을 구할 수 없으므로 걸러낸다.
                if (i > 0 && (points[i] - _points[_points.Count - 1]).sqrMagnitude <= Mathf.Epsilon)
                {
                    continue;
                }

                _points.Add(points[i]);
            }
        }

        float total = 0f;
        for (int i = 0; i < _points.Count; i++)
        {
            if (i > 0)
            {
                total += (_points[i] - _points[i - 1]).magnitude;
            }

            _cumulativeLength.Add(total);
        }

        _thickness = Mathf.Max(MIN_THICKNESS, thickness);
        _dashTexture = dashTexture;
        _dashPeriod = Mathf.Max(1f, dashPeriod);

        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (_points.Count < 2 || _progress <= 0f)
        {
            return;
        }

        float half = _thickness * 0.5f;
        Color32 vertexColor = color;
        float limit = _cumulativeLength[_cumulativeLength.Count - 1] * _progress;

        int ribCount = 0;
        while (ribCount < _points.Count && _cumulativeLength[ribCount] <= limit)
        {
            AddRib(vh, _points[ribCount], ResolveNormal(ribCount), _cumulativeLength[ribCount],
                half, vertexColor);
            ribCount++;
        }

        // 잘린 자리가 점과 점 사이면 그 자리에 끝을 하나 더 놓는다 - 없으면 채움이 점 단위로 튄다.
        if (ribCount > 0 && ribCount < _points.Count)
        {
            float previous = _cumulativeLength[ribCount - 1];
            float span = _cumulativeLength[ribCount] - previous;
            float t = span <= Mathf.Epsilon ? 0f : (limit - previous) / span;

            AddRib(vh, Vector2.Lerp(_points[ribCount - 1], _points[ribCount], t),
                ResolveTipNormal(ribCount, t), limit, half, vertexColor);
            ribCount++;
        }

        for (int i = 0; i < ribCount - 1; i++)
        {
            int v = i * 2;
            vh.AddTriangle(v, v + 1, v + 3);
            vh.AddTriangle(v, v + 3, v + 2);
        }
    }

    // 리본의 가로 한 칸(선 양옆 정점 2개).
    private void AddRib(
        VertexHelper vh, Vector2 point, Vector2 normal, float length, float half, Color32 vertexColor)
    {
        float u = length / _dashPeriod;

        vh.AddVert((Vector3)(point + normal * half), vertexColor, new Vector2(u, 0f));
        vh.AddVert((Vector3)(point - normal * half), vertexColor, new Vector2(u, 1f));
    }

    // 잘린 끝은 앞뒤 법선 사이에 있다. 두 법선은 단위 벡터이고 방향 차이도 작아 보간으로 충분하다.
    private Vector2 ResolveTipNormal(int index, float t)
    {
        Vector2 normal = Vector2.Lerp(ResolveNormal(index - 1), ResolveNormal(index), t);

        return normal.sqrMagnitude <= Mathf.Epsilon ? ResolveNormal(index) : normal.normalized;
    }

    // 이음매가 각지지 않게 앞뒤 방향을 평균한 접선의 수직을 쓴다.
    private Vector2 ResolveNormal(int index)
    {
        Vector2 tangent;

        if (index == 0)
        {
            tangent = _points[1] - _points[0];
        }
        else if (index == _points.Count - 1)
        {
            tangent = _points[index] - _points[index - 1];
        }
        else
        {
            tangent = (_points[index + 1] - _points[index - 1]);
        }

        if (tangent.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector2.up;
        }

        tangent.Normalize();
        return new Vector2(-tangent.y, tangent.x);
    }
}
