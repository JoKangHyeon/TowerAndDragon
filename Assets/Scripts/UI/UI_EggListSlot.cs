using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 알 리스트(Panel_BabyDragon/Scroll View_EggInventory)의 슬롯 하나 - 알 아이콘과 부화 진행도를 표시한다.
// 목록 구성·데이터 조회는 UI_DragonWindow가 하고 이 뷰는 Setup으로 값만 채운다
// (UI_BabyDragonListSlot과 동일한 값-주입 패턴).
public class UI_EggListSlot : MonoBehaviour
{
    // 부화까지 남은 일수. 데이터가 일(day) 단위뿐이라 초·분 표기는 만들 수 없다
    // (DragonEggInventorySystem이 OnDayStart마다 FedDayCount를 1씩 올린다).
    // baby_dragon_egg_progress("{0}/{1}일")를 재사용하지 않는 이유: 그 키는 기존 BabyDragon_window가
    // 인자 2개(먹은 일수/필요 일수)로 쓰고 있어 값을 바꾸면 그쪽 표시가 깨진다.
    private const string HATCH_TIME_LEFT_LOC_KEY = "baby_dragon_egg_time_left";

    [Tooltip("Icon_egg 의 Image.")]
    [SerializeField] private Image _icon;

    [Tooltip("Panel_Time/Text (TMP) - 부화까지 남은 일수.")]
    [SerializeField] private TMP_Text _hatchProgressText;

    // 알 스프라이트는 속성별로 BabyDragonData(Egg Growth)가 들고 있다 - 부화 일수와 같은 출처다.
    // 속성마다 이미지가 다르므로 색 틴트로 구분하지 않는다.
    // EggSprite가 비어 있으면 프리팹에 지정된 스프라이트를 그대로 둔다.
    public void Setup(DragonEgg egg, BabyDragonData data)
    {
        if (_icon != null)
        {
            if (data.EggSprite != null)
            {
                _icon.sprite = data.EggSprite;
            }

            _icon.color = Color.white;
        }

        if (_hatchProgressText != null)
        {
            // 초기 지급 알처럼 FedDayCount가 이미 목표치를 넘긴 경우 음수가 나오지 않도록 0에서 멈춘다.
            int daysLeft = Mathf.Max(0, data.DaysToHatch - egg.FedDayCount);
            _hatchProgressText.text = string.Format(
                StringTable.GetString(HATCH_TIME_LEFT_LOC_KEY), daysLeft);
        }
    }
}
