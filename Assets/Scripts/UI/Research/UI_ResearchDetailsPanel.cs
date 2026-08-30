using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 창의 상세 패널 - 선택된 노드의 이름·설명·상태·RP/자원 코스트를 표시하고 연구 버튼을 연결한다.
// 코스트 칸은 UI_DragonSkillDetailsPanel과 동일하게 UI_ResourceCostSlot을 필요한 개수만 인스턴스화한다.
public class UI_ResearchDetailsPanel : MonoBehaviour
{
    private const string PREREQUISITE_SEPARATOR = ", ";

    // 열고 닫을 때의 스케일 연출. 0이 아니라 살짝 작은 값에서 시작해 "튀어나온다"는 느낌만 준다 -
    // 0에서 키우면 창이 한 점에서 자라나 다른 창처럼 보인다.
    private const float POP_SCALE = 0.85f;

    // 코스트 줄이 상자 안에서 차지해도 되는 비율. 1이면 슬롯이 상자 테두리에 딱 붙는다.
    private const float COST_ROW_FILL_RATIO = 0.95f;

    private const float OPEN_DURATION = 0.18f;
    private const float CLOSE_DURATION = 0.12f;

    [SerializeField] private GameObject _root;

    [Tooltip("선택한 노드의 아이콘. 아이콘을 지정하지 않은 노드에서는 통째로 끈다 - " +
        "스프라이트 없는 Image는 흰 사각형이 되어 슬롯을 덮는다.")]
    [SerializeField] private Image _icon;

    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TextMeshProUGUI _statusText;
    [SerializeField] private TextMeshProUGUI _researchPointCostText;
    [SerializeField] private Transform _costContainer;
    [SerializeField] private UI_ResourceCostSlot _costSlotPrefab;
    [SerializeField] private Button _researchButton;
    [SerializeField] private TextMeshProUGUI _researchButtonText;
    [SerializeField] private Color _sufficientColor = Color.white;
    [SerializeField] private Color _insufficientColor = Color.red;

    private readonly List<UI_ResourceCostSlot> _costSlots = new();

    private ResearchManager _researchManager;
    private ResourceManager _resourceManager;
    private ResearchNodeData _selectedNode;

    /// <summary>
    /// 연구 창의 전체 갱신(UI_ResearchWindow.RefreshAll)을 되부르는 콜백.
    ///
    /// <b>이 콜백은 <see cref="HandleResearchClicked"/>에서만 호출한다.</b>
    /// RefreshAll이 이 패널의 <see cref="Refresh"/>를 부르므로, Refresh에서 이 콜백을 부르면
    /// <c>RefreshAll → Refresh → _onChanged → RefreshAll</c>로 무한 재귀가 된다.
    /// 연구 시스템에서 유일하게 순환이 될 수 있는 지점이라 <see cref="_isRefreshing"/>으로
    /// 실수까지 막아 둔다.
    /// </summary>
    private Action _onChanged;

    private bool _isRefreshing;

    private Tween _scaleTween;

    // 닫는 연출이 도는 동안에는 아직 켜져 있지만 이미 닫힌 것으로 친다 - 그러지 않으면
    // Esc를 연달아 눌렀을 때 두 번째 Esc가 창을 닫지 않고 닫는 중인 패널을 또 닫는다.
    private bool _isClosing;

    /// <summary>
    /// 지금 화면에 떠 있는지. 연구 창이 Esc를 창 닫기에 쓸지 이 패널 닫기에 쓸지 정하는 데 본다
    /// (<see cref="UI_ResearchWindow.OnCloseActionPerformed"/>).
    /// </summary>
    public bool IsShown => _root != null && _root.activeSelf && !_isClosing;

    public void Construct(
        ResearchManager researchManager,
        ResourceManager resourceManager,
        Action onChanged)
    {
        _researchManager = researchManager;
        _resourceManager = resourceManager;
        _onChanged = onChanged;

        ApplyLocalizedTexts();

        if (_researchButton != null)
        {
            _researchButton.onClick.AddListener(HandleResearchClicked);
        }

        // 시작 시 숨기는 것은 연출 대상이 아니다 - 뜬 적도 없는 패널이 줄어들며 사라진다.
        Clear(animate: false);
    }

    /// <summary>
    /// 한 번 써 놓고 마는 글자를 현재 언어로 다시 칠한다. 나머지 글자(이름·설명·상태·코스트)는
    /// <see cref="Refresh"/>가 매번 다시 읽으므로 언어를 저절로 따라간다.
    /// 언어 변경 구독은 연구 창이 맡는다(UI_ResearchWindow.ApplyLocalizedTexts).
    /// </summary>
    public void ApplyLocalizedTexts()
    {
        if (_researchButtonText != null)
        {
            _researchButtonText.text = StringTable.GetString(ResearchLocKeys.RESEARCH_BUTTON);
        }
    }

    /// <summary>
    /// 패널을 닫는다. <paramref name="animate"/>가 false면 즉시 끈다 -
    /// 창 전체가 함께 닫히는 경로에서는 연출이 끝나기 전에 오브젝트가 꺼져
    /// 스케일이 줄어든 채로 남는다.
    /// </summary>
    public void Clear(bool animate = true)
    {
        _selectedNode = null;

        if (_root == null)
        {
            return;
        }

        _scaleTween?.Kill();

        if (!animate || !_root.activeSelf)
        {
            HideImmediately();
            return;
        }

        _isClosing = true;
        _scaleTween = _root.transform
            .DOScale(POP_SCALE, CLOSE_DURATION)
            .SetEase(Ease.InBack)
            .SetLink(_root)
            .OnComplete(HideImmediately);
    }

    public void Show(ResearchNodeData node)
    {
        _selectedNode = node;

        if (_root != null)
        {
            // 닫는 중이었다면 그 연출을 버리고 다시 띄운다.
            _scaleTween?.Kill();
            _isClosing = false;

            _root.SetActive(true);
            _root.transform.localScale = Vector3.one * POP_SCALE;

            _scaleTween = _root.transform
                .DOScale(1f, OPEN_DURATION)
                .SetEase(Ease.OutBack)
                .SetLink(_root);
        }

        Refresh();
    }

    // 다음에 열 때 제 크기로 뜨도록 스케일까지 되돌린다.
    private void HideImmediately()
    {
        _isClosing = false;
        _root.transform.localScale = Vector3.one;
        _root.SetActive(false);
    }

    public void Refresh()
    {
        if (_selectedNode == null || _root == null || !_root.activeSelf || _researchManager == null)
        {
            return;
        }

        // 재진입만 막는다 - 갱신이 끝난 뒤 다시 부르는 것은 재진입이 아니라 그대로 동작한다.
        // (막는 대상은 _onChanged 설명에 적은 RefreshAll -> Refresh -> _onChanged 순환이다)
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;

        try
        {
            RefreshContents();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void RefreshContents()
    {
        if (_icon != null)
        {
            bool hasIcon = _selectedNode.Icon != null;
            _icon.gameObject.SetActive(hasIcon);

            if (hasIcon)
            {
                _icon.sprite = _selectedNode.Icon;
            }
        }

        if (_nameText != null)
        {
            _nameText.text = StringTable.GetString(_selectedNode.NameLocKey);
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = StringTable.GetString(_selectedNode.DescriptionLocKey);
        }

        ResearchNodeState state = _researchManager.GetNodeState(_selectedNode);

        if (_statusText != null)
        {
            _statusText.text = BuildStatusText(state);
        }

        if (_researchPointCostText != null)
        {
            _researchPointCostText.text = string.Format(
                StringTable.GetString(ResearchLocKeys.RP_COST),
                _selectedNode.ResearchPointCost);

            bool hasEnoughPoints =
                _researchManager.ResearchPoints >= _selectedNode.ResearchPointCost;
            _researchPointCostText.color = hasEnoughPoints ? _sufficientColor : _insufficientColor;
        }

        RebuildCostSlots();

        if (_researchButton != null)
        {
            _researchButton.interactable = state == ResearchNodeState.Available;
        }
    }

    private string BuildStatusText(ResearchNodeState state)
    {
        if (state != ResearchNodeState.PrerequisiteLocked)
        {
            return StringTable.GetString(ResearchLocKeys.ResolveStateLocKey(state));
        }

        string prerequisiteNames = BuildMissingPrerequisiteNames();
        if (string.IsNullOrEmpty(prerequisiteNames))
        {
            return StringTable.GetString(ResearchLocKeys.STATE_PREREQUISITE_LOCKED);
        }

        return string.Format(
            StringTable.GetString(ResearchLocKeys.STATE_PREREQUISITE_LOCKED_WITH_NAMES),
            prerequisiteNames);
    }

    private string BuildMissingPrerequisiteNames()
    {
        if (_selectedNode == null)
        {
            return string.Empty;
        }

        List<string> names = new();
        foreach (ResearchNodeData prerequisite in _selectedNode.Prerequisites)
        {
            if (prerequisite == null)
            {
                continue;
            }

            if (_researchManager != null && _researchManager.IsCompleted(prerequisite.NodeId))
            {
                continue;
            }

            names.Add(StringTable.GetString(prerequisite.NameLocKey));
        }

        return string.Join(PREREQUISITE_SEPARATOR, names);
    }

    /// <summary>
    /// 코스트 슬롯을 <b>파괴·재생성하지 않고 재사용한다.</b>
    ///
    /// 예전에는 Refresh마다 전부 Destroy하고 다시 Instantiate했다. Destroy는 프레임 끝으로
    /// 지연되는데 Instantiate는 즉시라, 한 프레임 동안 구 슬롯과 신 슬롯이 컨테이너에 같이 남아
    /// 코스트 아이콘이 여러 벌 겹쳐 보였다 - 연구 한 번에 Refresh가 대여섯 번
    /// (자원별 ResourceChanged + RP 변경 + NodeCompleted + 직접 호출) 돌기 때문에 눈에 띄었다.
    ///
    /// 아이콘은 항상 있다고 본다(자원 카탈로그의 RD_* 전부에 아이콘이 배선돼 있다).
    /// <see cref="UI_ResourceCostSlot.Setup"/>은 icon이 null이면 기존 스프라이트를 그대로 두므로,
    /// 아이콘 없는 자원이 생기면 재사용된 슬롯에 앞 자원의 아이콘이 남는다.
    /// </summary>
    private void RebuildCostSlots()
    {
        if (_costContainer == null || _costSlotPrefab == null)
        {
            return;
        }

        // 바깥에서 파괴된 슬롯이 섞이면 그 자리 코스트가 통째로 안 그려진다 - 먼저 걸러낸다.
        _costSlots.RemoveAll(slot => slot == null);

        IReadOnlyList<ResourceAmount> costs = _selectedNode.ResourceCost;

        while (_costSlots.Count < costs.Count)
        {
            _costSlots.Add(Instantiate(_costSlotPrefab, _costContainer));
        }

        for (int i = 0; i < _costSlots.Count; i++)
        {
            UI_ResourceCostSlot slot = _costSlots[i];
            bool isUsed = i < costs.Count;
            slot.gameObject.SetActive(isUsed);

            if (!isUsed)
            {
                continue;
            }

            ResourceAmount cost = costs[i];

            Sprite icon = null;
            if (_resourceManager != null && _resourceManager.Catalog != null &&
                _resourceManager.Catalog.TryGet(cost.Type, out ResourceData resourceData))
            {
                icon = resourceData.Icon;
            }

            bool hasEnough = _resourceManager != null &&
                _resourceManager.GetAmount(cost.Type) >= cost.Amount;
            Color textColor = hasEnough ? _sufficientColor : _insufficientColor;

            slot.Setup(icon, Color.white, cost.Amount.ToString(), textColor);
        }

        FitCostRowToBox();
    }

    /// <summary>
    /// 코스트 줄이 상자를 넘치면 줄 전체를 축소해 맞춘다.
    ///
    /// 슬롯은 폭이 고정이다(아이콘 50 + 숫자칸 50). 자원이 3종이 되면 줄이 상자보다 넓어져
    /// 마지막 숫자가 상자 밖으로 삐져나온다.
    ///
    /// 슬롯 프리팹(Slot_CostToConquer)을 줄이지 않는 이유: 점령 창·드래곤 스킬 창과 공유하는데,
    /// 점령 창만 숫자를 "보유/필요"(예: 120/200)로 찍는다. 숫자칸을 줄이면 그쪽에서 글자가
    /// 칸을 넘어 옆 슬롯과 겹친다(슬롯 내부 레이아웃이 childControlWidth = false라
    /// 글자가 길어져도 슬롯이 넓어지지 않는다).
    ///
    /// 그래서 공유물은 그대로 두고 이 패널에서만 줄을 축소한다. 자원이 2종 이하면 아무 일도 없다.
    /// </summary>
    private void FitCostRowToBox()
    {
        if (_costContainer is not RectTransform row ||
            row.parent is not RectTransform box)
        {
            return;
        }

        // 직전 호출에서 줄인 채로 재면 축소가 누적된다 - 원래 크기로 되돌리고 잰다.
        row.localScale = Vector3.one;
        LayoutRebuilder.ForceRebuildLayoutImmediate(row);

        float contentWidth = LayoutUtility.GetPreferredWidth(row);
        float availableWidth = box.rect.width * COST_ROW_FILL_RATIO;

        if (contentWidth <= availableWidth || contentWidth <= 0f)
        {
            return;
        }

        float scale = availableWidth / contentWidth;
        row.localScale = new Vector3(scale, scale, 1f);
    }

    private void HandleResearchClicked()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_selectedNode == null || _researchManager == null)
        {
            return;
        }

        _researchManager.TryResearch(_selectedNode, out ResearchFailureReason _);
        Refresh();
        _onChanged?.Invoke();
    }
}
