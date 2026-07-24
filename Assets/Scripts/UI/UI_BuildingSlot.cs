using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 빌드모드 창의 건물 슬롯 하나. 클릭하면 자신이 나타내는 건물을 선택하고,
// 이름 텍스트에 건물 정보를 표시한다. (아이콘/스탯 등은 추후 추가 예정)
[RequireComponent(typeof(Button))]
public class UI_BuildingSlot : MonoBehaviour
{
    [Tooltip("건물 이름을 표시할 텍스트 (Slot의 Text_name).")]
    [SerializeField]
    private TMP_Text _nameText;

    [Tooltip("건물 아이콘을 표시할 이미지 (Slot의 icon).")]
    [SerializeField]
    private Image _iconImage;

    private Building _prefab;
    private Action<Building> _onSelected;

    // 슬롯 생성 직후 스포너가 호출: 이 슬롯이 나타내는 건물과 클릭 콜백을 주입한다.
    public void Setup(Building prefab, Action<Building> onSelected)
    {
        _prefab = prefab;
        _onSelected = onSelected;

        Button button = GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onSelected?.Invoke(_prefab));

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

        return string.Empty;
    }
}
