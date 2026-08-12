using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 빌드모드 생산건물 슬롯에 마우스를 올렸을 때 슬롯 오른쪽에 뜨는 정보 창(Popup_Factory_Info).
// 위치 계산과 열고 닫기는 UI_BuildSlotInfoPopup이 담당하고, 여기서는 내용만 채운다.
public class UI_FactoryInfoPopup : UI_BuildSlotInfoPopup
{
    // 생산량·설명 키는 건물 프리팹 이름에서 만든다
    // (BasicFactory_FarmField → buildMode_panel_factory_Production_farmField / _info_farmField).
    private const string PRODUCTION_LOC_KEY_PREFIX = "buildMode_panel_factory_Production_";
    private const string INFO_LOC_KEY_PREFIX = "buildMode_panel_factory_info_";
    private const string BASIC_FACTORY_PREFAB_PREFIX = "BasicFactory_";
    private const string FACTORY_PREFAB_PREFIX = "Factory_";

    [Tooltip("Resource/Icon_mask/Icon (1) 의 Image - 생산하는 자원의 아이콘. " +
        "여러 자원을 내는 건물(슬라임 농장)에서는 이것을 원본 삼아 필요한 수만큼 복제한다.")]
    [SerializeField] private Image _resourceIcon;

    [Tooltip("Resource/Text (TMP) - 생산량 문구.")]
    [SerializeField] private TMP_Text _productionText;

    [Tooltip("Info/Text (TMP) - 건물별 설명문.")]
    [SerializeField] private TMP_Text _infoText;

    [Tooltip("생산 자원의 아이콘을 조회할 카탈로그 에셋(UI_BuildingSlot과 같은 출처).")]
    [SerializeField] private ResourceCatalog _resourceCatalog;

    // 자원 종류 수만큼 아이콘을 켠다. 프리팹에 놓인 Icon (1)을 0번으로 물려받아,
    // 자원이 하나뿐인 건물에서는 복제가 아예 일어나지 않는다.
    //
    // Awake가 아니라 첫 사용 시점에 만든다 - 이 팝업은 비활성으로 저장돼 있어 Awake가 첫 Show
    // 도중에야 돌고, Awake에서 만들면 그 첫 Show에서는 아직 풀이 없어 아이콘이 비어 나온다.
    private ComponentPool<Image> _iconPool;

    private ComponentPool<Image> IconPool
    {
        get
        {
            if (_iconPool == null && _resourceIcon != null)
            {
                _iconPool = new ComponentPool<Image>(
                    _resourceIcon, _resourceIcon.transform.parent, _resourceIcon);
            }

            return _iconPool;
        }
    }

    /// <summary>생산건물 정보를 채우고 슬롯 오른쪽에 띄운다.</summary>
    public void Show(Factory factory, RectTransform slotRect)
    {
        if (factory == null)
        {
            Hide();
            return;
        }

        Render(factory);
        ShowAt(slotRect);
    }

    private void Render(Factory factory)
    {
        // 프리팹 인스턴스가 아니라 목록에 등록된 프리팹 자체라 이름이 그대로 키가 된다.
        string prefabName = factory.name;

        if (_productionText != null)
        {
            _productionText.text = StringTable.GetString(
                LocKeySuffix.Build(
                    PRODUCTION_LOC_KEY_PREFIX,
                    prefabName,
                    BASIC_FACTORY_PREFAB_PREFIX,
                    FACTORY_PREFAB_PREFIX));
        }

        if (_infoText != null)
        {
            _infoText.text = StringTable.GetString(
                LocKeySuffix.Build(
                    INFO_LOC_KEY_PREFIX,
                    prefabName,
                    BASIC_FACTORY_PREFAB_PREFIX,
                    FACTORY_PREFAB_PREFIX));
        }

        RenderIcon(factory);
    }

    // 이 건물이 생산하는 자원마다 아이콘을 하나씩 켠다 - 슬라임 농장은 지형별 슬라임 5종을 모두 낸다.
    // 아이콘이 가로로 늘어서는 것은 Icon_mask의 HorizontalLayoutGroup이 담당하고,
    // 칸 수에 맞춰 폭이 늘어나는 것은 ContentSizeFitter가 담당한다.
    private void RenderIcon(Factory factory)
    {
        ComponentPool<Image> pool = IconPool;

        if (pool == null ||
            !WiringGuard.Require(_resourceCatalog, nameof(_resourceCatalog), this))
        {
            return;
        }

        int used = 0;

        foreach (ResourceType resourceType in factory.EnumerateProducedResourceTypes())
        {
            if (!_resourceCatalog.TryGet(resourceType, out ResourceData data) || data.Icon == null)
            {
                continue;
            }

            Image icon = pool.Get(used);
            icon.sprite = data.Icon;
            icon.enabled = true;
            used++;
        }

        // 이번에 쓰지 않은 칸은 숨긴다. 아이콘을 하나도 못 찾았으면 0번까지 꺼져
        // 프리팹의 자리표시 그림이 남지 않는다.
        pool.DeactivateFrom(used);
    }
}
