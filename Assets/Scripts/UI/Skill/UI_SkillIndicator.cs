using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스킬 HUD 아이콘 1개의 View. 아이콘/쿨타임 라디얼/남은 사용 횟수를 표시하고 클릭을 상위로 전달한다.</summary>
public class UI_SkillIndicator : MonoBehaviour
{
    [SerializeField] private Image _skillIconImage;
    [SerializeField] private Image _skillCooltimeFillImage;
    [SerializeField] private TMP_Text _skillRemainText;
    [Tooltip("클릭 입력을 받을 버튼 - SkillIcon 프리팹은 MultiImageButton을 사용한다.")]
    [SerializeField] private Button _button;

    private Skill _skill;
    private Action<Skill> _onClicked;

    // SkillManager 등 상위가 스킬 인스턴스를 주입할 때 호출한다.
    public void Bind(Skill skill, Action<Skill> onClicked)
    {
        _skill = skill;
        _onClicked = onClicked;

        if (_skillIconImage != null)
        {
            _skillIconImage.sprite = skill != null ? skill.Icon : null;
        }

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClicked);
        }

        Render();
    }

    private void Update()
    {
        Render();
    }

    private void Render()
    {
        if (_skill == null)
            return;

        if (!_skill.IsUsePerDayLeft)
        {
            if (_skillCooltimeFillImage != null)
            {
                _skillCooltimeFillImage.fillAmount = 1f;
            }

            if (_button != null)
            {
                _button.interactable = false;
            }

            if (_skillRemainText != null)
            {
                _skillRemainText.text = _skill.IsUnlimitedUse ? string.Empty : _skill.UsePerDayLeft.ToString();
            }

            return;
        }

        if (_button != null)
        {
            _button.interactable = _skill.CanUse;
        }

        if (_skillCooltimeFillImage != null)
        {
            _skillCooltimeFillImage.fillAmount = _skill.CooltimeRatio;
        }

        if (_skillRemainText != null)
        {
            _skillRemainText.text = _skill.IsUnlimitedUse ? string.Empty : _skill.UsePerDayLeft.ToString();
        }
    }

    private void HandleClicked()
    {
        _onClicked?.Invoke(_skill);
    }
}
