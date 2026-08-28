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

    [Tooltip("호버하면 스킬 이름·설명을 띄우는 트리거. 비우면 이 칸은 호버해도 아무것도 뜨지 않는다.")]
    [WiringOptional]
    [SerializeField] private UI_TooltipTrigger _tooltipTrigger;

    private Skill _skill;
    private Action<Skill> _onClicked;
    private DragonTreeManager _dragonTreeManager;

    // SkillHudBinder가 재바인딩 전에 한 번 주입한다 - 프리팹끼리 직접 참조를 걸 수 없는
    // UI_TooltipPresenter(다른 창 소속)를 상위가 대신 넘겨준다. DragonTreeManager는 툴팁이
    // 강화 노드 보너스를 반영한 "현재 적용" 수치를 읽는 데 쓴다(SkillTooltipBuilder.Build).
    public void Construct(UI_TooltipPresenter presenter, DragonTreeManager dragonTreeManager)
    {
        _tooltipTrigger?.SetPresenter(presenter);
        _dragonTreeManager = dragonTreeManager;
    }

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

        // 프로바이더는 호버할 때마다 다시 평가되므로, 재바인딩으로 _skill이 바뀌어도 다음 호버부터
        // 곧바로 최신 스킬을 읽는다 - 매 Bind마다 다시 걸 필요가 없다.
        _tooltipTrigger?.SetContentProvider(() => SkillTooltipBuilder.Build(_skill?.Data, _dragonTreeManager));

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
