using UnityEngine;

// 랜드마크 위에 뜨는 아이콘. 점령 여부·가동 여부를 색으로 구분한다.
//
// ExpeditionMarkerRenderer처럼 풀링하지 않는 이유: 원정은 매일 생겼다 사라지지만 랜드마크는
// 시작할 때 한 번 만들어지고 끝까지 그 자리에 있다. 풀을 두면 매 상태 변화마다 전체를 다시
// 배치하는 비용만 늘고 얻는 게 없다.
public sealed class LandmarkMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _iconRenderer;

    [Tooltip("아직 점령하지 않은 랜드마크의 아이콘 색. 흐리게 해서 미획득임을 알린다.")]
    [SerializeField] private Color _unconqueredColor = new Color(1f, 1f, 1f, 0.5f);

    [Tooltip("점령했지만 인구가 없는 랜드마크의 아이콘 색.")]
    [SerializeField] private Color _idleColor = Color.white;

    [Tooltip("인구가 배치되어 가동 중인 랜드마크의 아이콘 색.")]
    [SerializeField] private Color _operatingColor = new Color(1f, 0.9f, 0.5f);

    private Landmark _landmark;

    public void Bind(Landmark landmark)
    {
        _landmark = landmark;

        if (_iconRenderer != null && landmark.Data != null)
        {
            _iconRenderer.sprite = landmark.Data.Icon;
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_iconRenderer == null || _landmark == null)
        {
            return;
        }

        _iconRenderer.color = ResolveColor();
    }

    private Color ResolveColor()
    {
        if (!_landmark.IsConquered)
        {
            return _unconqueredColor;
        }

        return _landmark.IsOperating ? _operatingColor : _idleColor;
    }
}
