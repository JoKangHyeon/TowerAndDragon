using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 리스트 한 줄. 지형 아이콘 + 남은 일수를 표시한다(값은 UI_ClaimListWindow가 Setup으로 채운다).
public class UI_ClaimListSlot : MonoBehaviour
{
    private const string DAYS_LOC_KEY = "slot_claimList";
    private static string DaysFormat => StringTable.GetString(DAYS_LOC_KEY);

    private const float APPEAR_SLIDE_OFFSET_X = 100f;
    private const float APPEAR_DURATION = 0.5f;
    private const Ease APPEAR_EASE = Ease.OutQuad;

    [Tooltip("지형 아이콘.")]
    [SerializeField] private Image _terrainIcon;

    [Tooltip("남은 일수 텍스트.")]
    [SerializeField] private TMP_Text _daysText;

    [Tooltip("등장 시 우측에서 슬라이드인시킬 대상.")]
    [SerializeField] private RectTransform _appearTarget;

    private void Awake()
    {
        if (_appearTarget == null)
        {
            _appearTarget = transform as RectTransform;
        }
    }

    public void PlayAppearAnimation()
    {
        if (_appearTarget == null)
        {
            return;
        }

        _appearTarget.DOKill();
        _appearTarget.DOAnchorPos(new Vector2(APPEAR_SLIDE_OFFSET_X, 0f), APPEAR_DURATION)
            .From(isRelative: true)
            .SetEase(APPEAR_EASE)
            .SetLink(gameObject);
    }

    public void Setup(Sprite terrainIcon, int remainingDays)
    {
        if (_terrainIcon != null)
        {
            _terrainIcon.sprite = terrainIcon;
        }

        if (_daysText != null)
        {
            _daysText.text = string.Format(DaysFormat, remainingDays);
        }
    }
}
