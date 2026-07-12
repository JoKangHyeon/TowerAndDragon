using UnityEngine;
using DG.Tweening;

/// <summary>
/// 체력바에서 떨어져 나온 연출용 조각. 제자리에서 위로 조금 떠오르며(ease-out) 서서히 사라진 뒤 스스로 제거된다.
/// 상승·페이드는 DOTween 시퀀스로 처리한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class HealthChunkEffect : MonoBehaviour
{
    private Sequence _sequence;

    /// <summary>생성 직후 스포너가 호출한다. 이 시점부터 상승·페이드를 시작한다.</summary>
    public void Play(float lifetime, float riseDistance)
    {
        RectTransform rect = GetComponent<RectTransform>();
        CanvasGroup group = GetComponent<CanvasGroup>();

        group.alpha = 1f;

        // 수명이 없으면 연출 없이 즉시 제거.
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        float targetY = rect.anchoredPosition.y + riseDistance;

        _sequence = DOTween.Sequence();
        // 위로 상승: 처음엔 빠르게, 점점 느리게(ease-out).
        _sequence.Join(rect.DOAnchorPosY(targetY, lifetime).SetEase(Ease.OutQuad));
        // 페이드아웃: 처음엔 천천히, 끝으로 갈수록 빠르게 사라진다(ease-in).
        _sequence.Join(group.DOFade(0f, lifetime).SetEase(Ease.InQuad));
        // 오브젝트가 파괴되면 트윈도 자동 정리되도록 링크한다.
        _sequence.SetLink(gameObject);
        _sequence.OnComplete(() => Destroy(gameObject));
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
    }
}
