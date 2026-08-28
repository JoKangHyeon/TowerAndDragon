using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// TMP 텍스트 한 덩어리 안에서 "[스킬명]" 형태로 인용된 다른 스킬 이름을 호버 가능한 링크로 바꾼다.
/// 용 스킬트리 노드 설명문("액티브 [전역 빙결]을 해금합니다")과 튜토리얼 안내 말풍선
/// ("액티브 스킬 [성벽 재생]을 획득합니다")이 이 하나를 함께 쓴다.
///
/// UI_TooltipTrigger를 쓰지 않는 이유: 트리거는 오브젝트 하나당 툴팁 하나만 다루는데, 여기서는
/// 한 문단 안에 서로 다른 스킬을 가리키는 링크가 여러 개 있을 수 있다. 그래서 TMP 링크 태그 +
/// 포인터 위치로 "지금 어느 링크 위인가"를 직접 판별하고, 표시기(UI_TooltipPresenter)만 공유한다.
///
/// 모르는 대괄호(버튼 이름을 가리키는 "[건설 모드]", 미확정 표기 "[미정]" 등)는 스킬 이름
/// 사전에 없으므로 그대로 남는다 - 별도 이스케이프 규칙이 필요 없다.
/// </summary>
public class UI_SkillNameLinkTooltip : MonoBehaviour,
    IPointerEnterHandler,
    IPointerMoveHandler,
    IPointerExitHandler
{
    private const string BRACKET_PATTERN = @"\[([^\[\]]+)\]";
    private const string LINK_MARKUP_FORMAT = "<link=\"{0}\"><color=#{1}><u>[{2}]</u></color></link>";

    [SerializeField] private TMP_Text _text;

    [Tooltip("링크로 바꿀 스킬 이름의 출처. 비우면 대괄호가 전부 원문 그대로 남는다(링크 없음).")]
    [WiringOptional]
    [SerializeField] private DragonTreeManager _dragonTreeManager;

    [Tooltip("링크를 호버했을 때 그릴 표시기. 비우면 링크 색은 입지만 호버해도 툴팁이 뜨지 않는다.")]
    [WiringOptional]
    [SerializeField] private UI_TooltipPresenter _tooltipPresenter;

    [SerializeField] private Color _linkColor = new Color(1f, 0.82f, 0.35f);

    // 표시명(현재 언어) -> SkillSO. 대괄호 안 문자열을 그대로 찾아야 하므로 표시명이 키다.
    private readonly Dictionary<string, SkillSO> _skillsByDisplayName = new();

    // NameStringKey -> SkillSO. TMP 링크 태그의 id로 스킬을 되찾을 때 쓴다(표시명은 언어가 바뀌면
    // 달라지지만 태그에 박힌 id는 그대로다).
    private readonly Dictionary<string, SkillSO> _skillsByNameKey = new();

    private bool _indexBuilt;
    private int _hoveredLinkIndex = -1;

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += InvalidateIndex;
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= InvalidateIndex;
        _hoveredLinkIndex = -1;
        _tooltipPresenter?.Hide(this);
    }

    private void InvalidateIndex() => _indexBuilt = false;

    /// <summary>지역화된 원문을 받아, 알려진 스킬 이름을 감싼 [스킬명] 대괄호만 링크 마크업으로
    /// 바꾼다. 컴포넌트가 배선되지 않았거나 아는 스킬이 없으면 원문을 그대로 돌려준다.</summary>
    public string Decorate(string localizedText)
    {
        if (string.IsNullOrEmpty(localizedText))
        {
            return localizedText;
        }

        BuildIndexIfNeeded();

        if (_skillsByDisplayName.Count == 0)
        {
            return localizedText;
        }

        return Regex.Replace(localizedText, BRACKET_PATTERN, ReplaceKnownSkillName);
    }

    private string ReplaceKnownSkillName(Match match)
    {
        string bracketedName = match.Groups[1].Value;

        if (!_skillsByDisplayName.TryGetValue(bracketedName, out SkillSO skill))
        {
            return match.Value;
        }

        return string.Format(
            LINK_MARKUP_FORMAT,
            skill.NameStringKey,
            ColorUtility.ToHtmlStringRGB(_linkColor),
            bracketedName);
    }

    private void BuildIndexIfNeeded()
    {
        if (_indexBuilt)
        {
            return;
        }

        _skillsByDisplayName.Clear();
        _skillsByNameKey.Clear();

        if (_dragonTreeManager != null)
        {
            foreach (SkillSO skill in _dragonTreeManager.AllTreeSkills)
            {
                if (skill == null || string.IsNullOrEmpty(skill.NameStringKey))
                {
                    continue;
                }

                _skillsByDisplayName[StringTable.GetString(skill.NameStringKey)] = skill;
                _skillsByNameKey[skill.NameStringKey] = skill;
            }
        }

        _indexBuilt = true;
    }

    public void OnPointerEnter(PointerEventData eventData) => UpdateHover(eventData);

    public void OnPointerMove(PointerEventData eventData) => UpdateHover(eventData);

    public void OnPointerExit(PointerEventData eventData)
    {
        _hoveredLinkIndex = -1;
        _tooltipPresenter?.Hide(this);
    }

    private void UpdateHover(PointerEventData eventData)
    {
        if (_text == null || _tooltipPresenter == null)
        {
            return;
        }

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, ResolveCamera());

        if (linkIndex < 0)
        {
            if (_hoveredLinkIndex >= 0)
            {
                _hoveredLinkIndex = -1;
                _tooltipPresenter.Hide(this);
            }

            return;
        }

        if (linkIndex == _hoveredLinkIndex)
        {
            _tooltipPresenter.MoveTo(eventData.position);
            return;
        }

        _hoveredLinkIndex = linkIndex;

        BuildIndexIfNeeded();
        string linkId = _text.textInfo.linkInfo[linkIndex].GetLinkID();
        _skillsByNameKey.TryGetValue(linkId, out SkillSO skill);
        TooltipContent content = SkillTooltipBuilder.Build(skill, _dragonTreeManager);

        if (!content.HasContent)
        {
            _tooltipPresenter.Hide(this);
            return;
        }

        _tooltipPresenter.Show(this, content, eventData.position);
    }

    private Camera ResolveCamera()
    {
        Canvas canvas = _text != null ? _text.canvas : null;
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }
}
