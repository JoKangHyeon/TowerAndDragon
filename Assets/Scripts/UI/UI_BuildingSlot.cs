using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 빌드모드 창의 건물 슬롯 하나. 클릭하면 자신이 나타내는 건물을 선택하고,
// 이름 텍스트에 건물 정보와 건설 비용을 표시한다.
[RequireComponent(typeof(Button))]
public class UI_BuildingSlot : MonoBehaviour
{
    // 건설 비용 한 종류를 표시하는 칸(Stat/stat_resource_0N) 하나 - 인스펙터에서 4칸을 순서대로 연결한다.
    [Serializable]
    private struct CostStatSlot
    {
        public GameObject Root;
        public Image IconImage;
        public TMP_Text CountText;
    }

    [Tooltip("건물 이름을 표시할 텍스트 (Slot의 Text_name).")]
    [SerializeField]
    private TMP_Text _nameText;

    [Tooltip("건물 아이콘을 표시할 이미지 (Slot의 icon).")]
    [SerializeField]
    private Image _iconImage;

    [Tooltip("건설 비용을 표시할 칸(Stat/stat_resource_01~04). 최대 4종류까지 표시 가능.")]
    [SerializeField]
    private CostStatSlot[] _costStatSlots;

    [Tooltip("건설 비용 자원의 아이콘을 조회할 카탈로그 에셋.")]
    [SerializeField]
    private ResourceCatalog _resourceCatalog;

    [Tooltip("최대 할당 인원수를 표시할 텍스트 (Tower_stat/stat_people의 Text (TMP)).")]
    [SerializeField]
    private TMP_Text _populationCapacityText;

    private Building _prefab;
    private Action<Building> _onSelected;
    private Button _button;

    // 슬롯 생성 직후 스포너가 호출: 이 슬롯이 나타내는 건물과 클릭 콜백을 주입한다.
    public void Setup(Building prefab, Action<Building> onSelected)
    {
        _prefab = prefab;
        _onSelected = onSelected;

        _button = GetComponent<Button>();
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() =>
        {
            SoundManager.Play(SoundId.UiButtonClick);
            _onSelected?.Invoke(_prefab);
        });

        string displayName = ResolveName(prefab);
        if (_nameText != null && !string.IsNullOrEmpty(displayName))
        {
            _nameText.text = displayName;
        }

        // 건물 프리팹이 실제로 그리는 스프라이트(SpriteRenderer)를 슬롯 아이콘으로 표시.
        Sprite icon = ResolveIcon(prefab);
        if (_iconImage != null && icon != null)
        {
            _iconImage.sprite = icon;
        }

        ApplyBuildCost(prefab.BuildCost);

        if (_populationCapacityText != null)
        {
            _populationCapacityText.text = prefab.PopulationCapacity.ToString();
        }
    }

    // 밤에는 건설을 시작할 수 없으므로 슬롯을 회색으로 비활성화한다(UI_BuildModeWindow가 매 재오픈마다 호출).
    public void SetInteractable(bool interactable)
    {
        if (_button != null)
        {
            _button.interactable = interactable;
        }
    }

    // 건설 비용을 칸에 채운다. 항목 수만큼만 칸을 켜고 나머지는 꺼서
    // GridLayoutGroup이 비활성 칸을 건너뛰고 남은 칸끼리 자동으로 채우게 한다.
    private void ApplyBuildCost(IReadOnlyList<ResourceAmount> cost)
    {
        if (_costStatSlots == null)
            return;

        for (int i = 0; i < _costStatSlots.Length; i++)
        {
            CostStatSlot slot = _costStatSlots[i];
            bool hasEntry = i < cost.Count;

            if (slot.Root != null)
                slot.Root.SetActive(hasEntry);

            if (!hasEntry)
                continue;

            if (slot.IconImage != null)
                slot.IconImage.sprite = ResolveResourceIcon(cost[i].Type);

            if (slot.CountText != null)
                slot.CountText.text = cost[i].Amount.ToString();
        }
    }

    // 자원 아이콘은 데이터 에셋(ResourceCatalog)이 단일 출처 - 종류로 조회한다.
    private Sprite ResolveResourceIcon(ResourceType type)
    {
        if (_resourceCatalog != null && _resourceCatalog.TryGet(type, out ResourceData data))
            return data.Icon;

        return null;
    }

    // Buildings에 들어간 건물 프리팹의 실제 스프라이트(SpriteRenderer)를 얻는다.
    private static Sprite ResolveIcon(Building prefab)
    {
        SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    // 건물에서 표시할 이름을 얻는다.
    // (추후 스트링테이블이 생기면 StringTable.GetString(key)로 감싸 지역화하면 된다.)
    private static string ResolveName(Building prefab)
    {
        if (prefab is Tower tower && tower.Data != null)
            return StringTable.GetString(tower.Data.NameLocKey);

        if (prefab is Factory factory && factory.Data != null)
            return StringTable.GetString(factory.Data.NameLocKey);

        if (prefab is ResearchLab researchLab && researchLab.Data != null)
            return StringTable.GetString(researchLab.Data.NameLocKey);

        if (prefab is SealStone sealStone && sealStone.Data != null)
            return StringTable.GetString(sealStone.Data.NameLocKey);

        return string.Empty;
    }
}
