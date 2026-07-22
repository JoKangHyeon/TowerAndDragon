using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 점령 리스트 한 줄. 지형 아이콘 + 남은 일수를 표시한다(값은 UI_ClaimListWindow가 Setup으로 채운다).
public class UI_ClaimListSlot : MonoBehaviour
{
    private const string DAYS_FORMAT = "{0}days";

    [Tooltip("지형 아이콘.")]
    [SerializeField] private Image _terrainIcon;

    [Tooltip("남은 일수 텍스트.")]
    [SerializeField] private TMP_Text _daysText;

    [Tooltip("Slot appear animation component.")]
    [SerializeField] private DOTweenAnimation _appearAnimation;

    private void Awake()
    {
        if (_appearAnimation == null)
        {
            _appearAnimation = GetComponent<DOTweenAnimation>();
        }

        if (_appearAnimation == null)
        {
            _appearAnimation = GetComponentInChildren<DOTweenAnimation>(true);
        }
    }

    public void PlayAppearAnimation()
    {
        if (_appearAnimation == null)
        {
            return;
        }

        _appearAnimation.RecreateTweenAndPlay();
    }

    public void Setup(Sprite terrainIcon, int remainingDays)
    {
        if (_terrainIcon != null)
        {
            _terrainIcon.sprite = terrainIcon;
        }

        if (_daysText != null)
        {
            _daysText.text = string.Format(DAYS_FORMAT, remainingDays);
        }
    }
}
