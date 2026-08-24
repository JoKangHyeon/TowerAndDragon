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
[RequireComponent(typeof(CanvasRenderer))]
public sealed class UI_CurvedEdge : MaskableGraphic
{
    private const float MIN_THICKNESS = 0.5f;

    private readonly List<Vector2> _points = new();
    private readonly List<float> _cumulativeLength = new();

    private float _thickness = 4f;
    private float _dashPeriod = 12f;
    private Texture _dashTexture;

    public override Texture mainTexture => _dashTexture != null ? _dashTexture : base.mainTexture;

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

        if (_points.Count < 2)
        {
            return;
        }

        float half = _thickness * 0.5f;
        Color32 vertexColor = color;

        for (int i = 0; i < _points.Count; i++)
        {
            Vector2 normal = ResolveNormal(i);
            float u = _cumulativeLength[i] / _dashPeriod;

            vh.AddVert((Vector3)(_points[i] + normal * half), vertexColor, new Vector2(u, 0f));
            vh.AddVert((Vector3)(_points[i] - normal * half), vertexColor, new Vector2(u, 1f));
        }

        for (int i = 0; i < _points.Count - 1; i++)
        {
            int v = i * 2;
            vh.AddTriangle(v, v + 1, v + 3);
            vh.AddTriangle(v, v + 3, v + 2);
        }
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
