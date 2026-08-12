using UnityEngine;

// 랜드마크 위에 뜨는 아이콘. 점령 여부·가동 여부를 색으로 구분한다.
//
// ExpeditionMarkerRenderer처럼 풀링하지 않는 이유: 원정은 매일 생겼다 사라지지만 랜드마크는
// 시작할 때 한 번 만들어지고 끝까지 그 자리에 있다. 풀을 두면 매 상태 변화마다 전체를 다시
// 배치하는 비용만 늘고 얻는 게 없다.
public sealed class LandmarkMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _iconRenderer;

    // 속성별 알 아이콘을 "그 속성의 바이옴"에 놓는 구조라, 아이콘과 바닥이 같은 계열 색이 되는
    // 최악의 대비가 기본값이다(사막의 모래빛 시간알, 암석지대의 회색 돌알). 아이콘을 조금
    // 키우고 밝게 해도 이건 해결되지 않아, 뒤에 같은 모양의 어두운 실루엣을 깔아 윤곽을 만든다.
    [Tooltip("아이콘 뒤에 깔아 지형과 분리해 주는 실루엣. 비워두면 실루엣 없이 아이콘만 그린다.")]
    [SerializeField] private SpriteRenderer _outlineRenderer;

    [Tooltip("실루엣 색. 어떤 지형 위에서도 아이콘 윤곽이 읽히도록 어둡게 둔다.")]
    [SerializeField] private Color _outlineColor = new Color(0f, 0f, 0f, 0.65f);

    // 살짝 어둡게만 한다. 예전에는 알파 0.5였는데, 지형 위에서 아이콘이 거의 묻혀
    // "저기 뭔가 있다"를 읽을 수 없었다 - 미획득 표시는 알아볼 수 있는 선에서만 흐려야 한다.
    [Tooltip("아직 점령하지 않은 랜드마크의 아이콘 색. 살짝 어둡게 해서 미획득임을 알린다.")]
    [SerializeField] private Color _unconqueredColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);

    [Tooltip("점령했지만 인구가 없는 랜드마크의 아이콘 색.")]
    [SerializeField] private Color _idleColor = Color.white;

    [Tooltip("인구가 배치되어 가동 중인 랜드마크의 아이콘 색.")]
    [SerializeField] private Color _operatingColor = new Color(1f, 0.9f, 0.5f);

    private Landmark _landmark;

    public void Bind(Landmark landmark)
    {
        _landmark = landmark;

        if (landmark.Data != null)
        {
            if (_iconRenderer != null)
            {
                _iconRenderer.sprite = landmark.Data.Icon;
            }

            // 실루엣은 아이콘과 같은 스프라이트를 조금 크게 깐 것이라 스프라이트도 같이 물려준다.
            if (_outlineRenderer != null)
            {
                _outlineRenderer.sprite = landmark.Data.Icon;
            }
        }

        Refresh();
    }

    public void Refresh()
    {
        if (_iconRenderer == null || _landmark == null)
        {
            return;
        }

        // 아직 못 본 청크의 마커는 아예 끈다. 켜 두면 1일차부터 미탐색 지역의 랜드마크가
        // 안개를 뚫고 다 보여 탐색할 이유가 사라진다.
        Color iconColor = ResolveColor();

        _iconRenderer.enabled = _landmark.IsRevealed;
        _iconRenderer.color = iconColor;

        if (_outlineRenderer != null)
        {
            _outlineRenderer.enabled = _landmark.IsRevealed;

            // 실루엣도 아이콘과 같은 비율로 흐려져야 미점령 표시가 아이콘만 흐린 것처럼 보이지 않는다.
            var outlineColor = _outlineColor;
            outlineColor.a *= iconColor.a;
            _outlineRenderer.color = outlineColor;
        }
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
