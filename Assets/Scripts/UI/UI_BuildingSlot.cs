using System;
using UnityEngine;
using UnityEngine.UI;

// 빌드모드 창의 건물 슬롯 하나. 클릭하면 자신이 나타내는 건물을 선택한다.
// (아이콘/이름/스탯 등 타워 정보 표시는 추후 추가 예정)
[RequireComponent(typeof(Button))]
public class UI_BuildingSlot : MonoBehaviour
{
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
    }
}
