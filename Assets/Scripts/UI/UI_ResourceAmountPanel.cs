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
    [Tooltip("보유량·아이콘 카탈로그의 출처. 없으면 아무것도 표시하지 않는다.")]
    [SerializeField] private ResourceManager _resourceManager;

    [Tooltip("하루 예상 증감 표기용. 각 자원 보유량 옆에 (+증가) 또는 (-감소)로 노출한다.")]
    [FormerlySerializedAs("_productionForecast")]
    [SerializeField] private ResourceForecast _resourceForecast;

    [Tooltip("하루 순증가 글씨 색(연두색).")]
    [SerializeField] private Color _productionColor = ResourceAmountView.PRODUCTION_COLOR_DEFAULT;

    [Tooltip("하루 순감소 글씨 색(적색). 다음 정산 후 바닥나는 자원은 보유량까지 이 색으로 물든다.")]
    [SerializeField] private Color _lossColor = ResourceAmountView.LOSS_COLOR_DEFAULT;

    [Tooltip("표시할 자원 칸 목록.")]
    [SerializeField] private ResourceAmountSlot[] _slots;

    private ResourceAmountView _view;

    private void OnEnable()
    {
        _view ??= new ResourceAmountView(
            _resourceManager, _resourceForecast, _productionColor, _lossColor, _slots);
        _view.Subscribe();
        _view.RefreshLayoutNextFrame(this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void OnDisable()
    {
        _view?.Unsubscribe();
    }
}
