using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// 자원 보유량 표시 로직 - 슬롯 배열을 받아 구독·렌더·아이콘·레이아웃 보정을 담당한다.
// MonoBehaviour가 아니라 일반 클래스다: 슬롯 배열을 직렬화해 들고 있는 주체는 각 창/패널이고
// (UI_IngameWindow는 HUD 상단 7칸, UI_ResourceAmountPanel은 용 창 슬라임 5칸),
// 이 클래스는 그 배열을 넘겨받아 표시 방식만 통일한다.
//
// 표시 형식·아이콘 틴트·구독 타이밍이 창마다 어긋나지 않도록 여기 한 곳에만 둔다.
public class ResourceAmountView
{
    // 자원 표기: 보유량 + 하루 예상 생산량. 생산량은 "보유량(+생산량)" 형태(TMP 리치텍스트 컬러 태그).
    private const string AMOUNT_WITH_PRODUCTION_FORMAT = "{0}<color=#{1}>(+{2})</color>";

    // 하루 생산량 글씨 기본 색(연두색).
    public static readonly Color PRODUCTION_COLOR_DEFAULT = new Color(0.62f, 1f, 0.42f);

    private readonly ResourceManager _resourceManager;
    private readonly ProductionForecast _productionForecast;
    private readonly Color _productionColor;
    private readonly IReadOnlyList<ResourceAmountSlot> _slots;

    public ResourceAmountView(
        ResourceManager resourceManager,
        ProductionForecast productionForecast,
        Color productionColor,
        IReadOnlyList<ResourceAmountSlot> slots)
    {
        _resourceManager = resourceManager;
        _productionForecast = productionForecast;
        _productionColor = productionColor;
        _slots = slots;
    }

    // 구독 직후 현재 값을 한 번 반영한다 - 활성화 전에 발생한 변경(초기 지급분 포함)을 놓치지 않기 위함
    // (CLAUDE.md 이벤트 초기화 규칙).
    public void Subscribe()
    {
        if (_resourceManager == null || _slots == null)
        {
            return;
        }

        _resourceManager.ResourceChanged.AddListener(RenderOne);

        // 생산량 예측이 바뀌면(건물/인구 변경) 보유량 옆 (+생산량) 표기를 다시 그린다.
        if (_productionForecast != null)
        {
            _productionForecast.ForecastChanged.AddListener(RenderAll);
        }

        ApplyIcons();
        RenderAll();
    }

    public void Unsubscribe()
    {
        if (_resourceManager == null)
        {
            return;
        }

        _resourceManager.ResourceChanged.RemoveListener(RenderOne);

        if (_productionForecast != null)
        {
            _productionForecast.ForecastChanged.RemoveListener(RenderAll);
        }
    }

    // 활성화 다음 프레임(자원 패널·텍스트 크기 확정 시점)에 딱 한 번 실행.
    // 순서 보장: (1) 각 자원 패널을 먼저 갱신해 폭을 확정 → (2) 그 부모 층을 갱신해 확정된 폭으로 재배치.
    // ContentSizeFitter가 없는 패널(폭이 고정인 경우)은 그냥 건너뛰므로 어느 창에서든 그대로 호출해도 된다.
    // 토큰은 호출하는 MonoBehaviour가 GetCancellationTokenOnDestroy()로 넘긴다.
    public async UniTaskVoid RefreshLayoutNextFrame(CancellationToken cancellationToken)
    {
        if (_slots == null)
        {
            return;
        }

        await UniTask.NextFrame(cancellationToken);

        foreach (ResourceAmountSlot slot in _slots)
        {
            if (slot.AmountText == null)
            {
                continue;
            }

            ContentSizeFitter panelFitter = slot.AmountText.GetComponentInParent<ContentSizeFitter>();
            if (panelFitter == null)
            {
                continue;
            }

            RectTransform panelRect = (RectTransform)panelFitter.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            if (panelRect.parent is RectTransform floorRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(floorRect);
            }
        }
    }

    // 아이콘은 자원 종류마다 고정이라 보유량과 달리 활성화 시 1회만 채우면 된다.
    // 자원 아이콘은 데이터 에셋(ResourceData)이 단일 출처 - 카탈로그에서 종류로 조회한다.
    // 슬라임까지 전 자원이 고유 스프라이트를 가지므로 틴트하지 않는다(Color.white).
    private void ApplyIcons()
    {
        if (_resourceManager.Catalog == null)
        {
            return;
        }

        foreach (ResourceAmountSlot slot in _slots)
        {
            if (slot.IconImage == null || !_resourceManager.Catalog.TryGet(slot.Type, out ResourceData data))
            {
                continue;
            }

            slot.IconImage.sprite = data.Icon;
            slot.IconImage.color = Color.white;
        }
    }

    // 전 슬롯의 보유량을 현재 값으로 다시 그린다.
    private void RenderAll()
    {
        foreach (ResourceAmountSlot slot in _slots)
        {
            RenderOne(slot.Type, _resourceManager.GetAmount(slot.Type));
        }
    }

    private void RenderOne(ResourceType type, int amount)
    {
        foreach (ResourceAmountSlot slot in _slots)
        {
            if (slot.Type == type && slot.AmountText != null)
            {
                slot.AmountText.text = Format(type, amount);
            }
        }
    }

    // "보유량" 또는 하루 예상 생산량이 있으면 "보유량(+생산량)"(생산량만 색 지정)으로 만든다.
    private string Format(ResourceType type, int amount)
    {
        int production = _productionForecast != null ? _productionForecast.GetDailyProduction(type) : 0;
        if (production <= 0)
        {
            return amount.ToString();
        }

        return string.Format(
            AMOUNT_WITH_PRODUCTION_FORMAT,
            amount,
            ColorUtility.ToHtmlStringRGB(_productionColor),
            production);
    }
}
