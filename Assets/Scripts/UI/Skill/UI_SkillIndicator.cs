using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스킬 HUD 아이콘 1개의 View. 아이콘/쿨타임 라디얼/남은 사용 횟수를 표시하고 클릭을 상위로 전달한다.
/// 바인딩된 스킬이 없으면(미해금 또는 활성 속성 불일치) 슬롯 자체를 숨긴다 - 아이콘만 비우면
/// 프레임·쿨타임 라디얼이 남아 "빈 칸"이 그대로 보인다.</summary>
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
    // 비활성 GameObject의 컴포넌트에도 직접 호출할 수 있으므로, 숨긴 슬롯을 다시 켜는 것도 이 경로로 처리한다.
    public void Bind(Skill skill, Action<Skill> onClicked)
    {
        _skill = skill;
        _onClicked = onClicked;

        // 스킬이 없는 슬롯은 아예 보이지 않게 한다. HorizontalLayoutGroup이 남은 아이콘을 다시 정렬한다.
        gameObject.SetActive(skill != null);

        if (skill == null)
        {
            return;
        }

        if (_skillIconImage != null)
        {
            _skillIconImage.sprite = skill.Icon;
        }

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClicked);
        }

        Render();
    }

    public void ShowIfBinded()
    {
        if (_skill != null)
        {
            gameObject.SetActive(true);
        }
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
                _skillCooltimeFillImage.fillAmount = _skill.CooltimeRatio;
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
        SoundManager.Play(SoundId.UiButtonClick);
        _onClicked?.Invoke(_skill);
    }
}
