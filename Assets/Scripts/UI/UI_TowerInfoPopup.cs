using TMPro;
using UnityEngine;

// 빌드모드 타워 슬롯에 마우스를 올렸을 때 슬롯 오른쪽에 뜨는 정보 창(Popup_Tower_Info).
// 위치 계산과 열고 닫기는 UI_BuildSlotInfoPopup이 담당하고, 여기서는 내용만 채운다.
public class UI_TowerInfoPopup : UI_BuildSlotInfoPopup
{
    // "수용 인구: {0}" - {0}에 정원이 들어간다.
    private const string PEOPLE_LOC_KEY = "buildMode_panel_tower_people";

    // 타워별 설명 키는 TowerData 에셋 이름에서 만든다(TD_CrossBow → buildMode_panel_tower_info_crossBow).
    private const string INFO_LOC_KEY_PREFIX = "buildMode_panel_tower_info_";
    private const string TOWER_DATA_ASSET_PREFIX = "TD_";

    [Header("Stat_tower")]
    [Tooltip("hp/Text (TMP) - 최대 체력(TowerData.MaxHealth).")]
    [SerializeField] private TMP_Text _healthText;

    [Tooltip("attack/Text (TMP) - 공격력(AttackSO의 피해 효과 합).")]
    [SerializeField] private TMP_Text _attackText;

    [Tooltip("range/Text (TMP) - 사거리(AttackSO.Range).")]
    [SerializeField] private TMP_Text _rangeText;

    [Tooltip("attack 행 전체. 공격 데이터가 없는 타워에서는 통째로 숨긴다.")]
    [SerializeField] private GameObject _attackRow;

    [Tooltip("range 행 전체. 공격 데이터가 없는 타워에서는 통째로 숨긴다.")]
    [SerializeField] private GameObject _rangeRow;

    [Header("그 외")]
    [Tooltip("populationCapacity/Text (TMP) - 수용 인구.")]
    [SerializeField] private TMP_Text _populationText;

    [Tooltip("Info/Text (TMP) - 타워별 설명문.")]
    [SerializeField] private TMP_Text _infoText;

    /// <summary>타워 정보를 채우고 슬롯 오른쪽에 띄운다.</summary>
    public void Show(TowerData data, RectTransform slotRect)
    {
        if (data == null)
        {
            Hide();
            return;
        }

        Render(data);
        ShowAt(slotRect);
    }

    private void Render(TowerData data)
    {
        if (_healthText != null)
        {
            _healthText.text = Mathf.RoundToInt(data.MaxHealth).ToString();
        }

        if (_populationText != null)
        {
            _populationText.text = string.Format(
                StringTable.GetString(PEOPLE_LOC_KEY),
                data.PopulationCapacity);
        }

        if (_infoText != null)
        {
            _infoText.text = StringTable.GetString(InfoLocKeyOf(data));
        }

        RenderAttack(data.Attack);
    }

    // 공격 데이터가 없는 타워(TD_TimeTower 등)는 공격력·사거리 행을 통째로 숨긴다 -
    // 0을 보여주면 "공격력 0인 타워"로 읽혀 잘못된 정보가 된다.
    private void RenderAttack(AttackSO attack)
    {
        bool hasAttack = attack != null;

        if (_attackRow != null)
        {
            _attackRow.SetActive(hasAttack);
        }

        if (_rangeRow != null)
        {
            _rangeRow.SetActive(hasAttack);
        }

        if (!hasAttack)
        {
            return;
        }

        if (_attackText != null)
        {
            _attackText.text = Mathf.RoundToInt(SumDamage(attack)).ToString();
        }

        if (_rangeText != null)
        {
            _rangeText.text = attack.Range.ToString();
        }
    }

    // 한 번의 공격이 주는 피해량. 효과가 여러 개면 합산한다(피해 효과가 아닌 것은 건너뛴다).
    // 연구·용 스킬 보정은 반영하지 않는다 - 이 창은 타워 자체의 기본 수치를 보여준다.
    private static float SumDamage(AttackSO attack)
    {
        float total = 0f;

        foreach (AttackEffectSO effect in attack.Effects)
        {
            if (effect is DamageEffectSO damage)
            {
                total += damage.Amount;
            }
        }

        return total;
    }

    // 에셋 이름을 바꾸면 키가 어긋나지만, 그때는 StringTable이 키 문자열을 그대로 돌려주므로
    // 설명 자리에 "buildMode_panel_tower_info_..."가 그대로 보여 화면에서 바로 드러난다.
    private static string InfoLocKeyOf(TowerData data)
    {
        return LocKeySuffix.Build(INFO_LOC_KEY_PREFIX, data.name, TOWER_DATA_ASSET_PREFIX);
    }
}
