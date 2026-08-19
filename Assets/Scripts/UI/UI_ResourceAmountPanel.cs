using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

// 지정한 자원들의 보유량을 아이콘 + 수량(+하루 순증감)으로 표시하는 독립 패널.
// 용 창의 슬라임 현황(Panel_elementalResourceAmount)처럼, 창 스크립트와 분리된 패널에 붙여 쓴다.
//
// 표시 로직은 전부 ResourceAmountView가 갖고 있다(HUD 상단 자원 표시와 같은 구현) -
// 이 컴포넌트는 인스펙터 배선과 생명주기만 담당하는 얇은 래퍼다.
public class UI_ResourceAmountPanel : MonoBehaviour
{
    [Tooltip("보유량·아이콘 카탈로그의 출처. 비워두면 소유 창이 Construct로 주입해야 한다 " +
        "(HUD 안쪽 패널은 씬 참조를 프리팹에 박을 수 없어 그 경로를 쓴다). 둘 다 없으면 아무것도 표시하지 않는다.")]
    [WiringOptional]
    [SerializeField] private ResourceManager _resourceManager;

    [Tooltip("하루 예상 증감 표기용. 각 자원 보유량 옆에 (+증가) 또는 (-감소)로 노출한다. " +
        "비워두면 소유 창이 Construct로 주입해야 하고, 그것도 없으면 증감 표기 없이 보유량만 나온다.")]
    [WiringOptional]
    [FormerlySerializedAs("_productionForecast")]
    [SerializeField] private ResourceForecast _resourceForecast;

    [Tooltip("하루 순증가 글씨 색(연두색).")]
    [SerializeField] private Color _productionColor = ResourceAmountView.PRODUCTION_COLOR_DEFAULT;

    [Tooltip("하루 순감소 글씨 색(적색). 다음 정산 후 바닥나는 자원은 보유량까지 이 색으로 물든다.")]
    [SerializeField] private Color _lossColor = ResourceAmountView.LOSS_COLOR_DEFAULT;

    [Tooltip("표시할 자원 칸 목록.")]
    [SerializeField] private ResourceAmountSlot[] _slots;

    private ResourceAmountView _view;

    /// <summary>
    /// 자원 출처를 밖에서 주입한다. 씬 오브젝트를 참조해야 하는 두 필드를, 이미 배선을 갖고 있는
    /// 소유 창이 대신 넣어주는 경로다(UI_TooltipTrigger.SetPresenter와 같은 관례) - 프리팹 안쪽
    /// 패널까지 씬마다 손으로 이어주지 않기 위함이다. 인스펙터로 직접 배선한 패널은 호출하지 않으면 된다.
    ///
    /// 이미 만들어진 뷰는 헌 참조로 구독을 걸어둔 상태이므로 버리고 새로 만든다.
    /// </summary>
    public void Construct(ResourceManager resourceManager, ResourceForecast resourceForecast)
    {
        _resourceManager = resourceManager;
        _resourceForecast = resourceForecast;

        _view?.Unsubscribe();
        _view = null;

        // 켜져 있는 동안 주입받았다면 그 자리에서 다시 구독한다(꺼져 있으면 다음 OnEnable이 한다).
        if (isActiveAndEnabled)
        {
            BuildAndSubscribe();
        }
    }

    private void OnEnable()
    {
        BuildAndSubscribe();
    }

    private void OnDisable()
    {
        _view?.Unsubscribe();
    }

    private void BuildAndSubscribe()
    {
        _view ??= new ResourceAmountView(
            _resourceManager, _resourceForecast, _productionColor, _lossColor, _slots);
        _view.Subscribe();
        _view.RefreshLayoutNextFrame(this.GetCancellationTokenOnDestroy()).Forget();
    }
}
