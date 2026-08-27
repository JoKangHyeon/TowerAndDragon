using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

// 빌드모드 타워 슬롯에 마우스를 올렸을 때 슬롯 오른쪽에 뜨는 정보 창(Popup_Tower_Info).
// 위치 계산과 열고 닫기는 UI_BuildSlotInfoPopup이 담당하고, 여기서는 내용만 채운다.
public class UI_TowerInfoPopup : UI_BuildSlotInfoPopup
{
    // "수용 인구: {0}" - {0}에 정원이 들어간다.
    private const string PEOPLE_LOC_KEY = "buildMode_panel_tower_people";

    // 시간 단위는 언어마다 붙는 자리가 달라 서식을 스트링테이블에 둔다("1.25초" / "1.25s").
    // (UI_PopulationAllocationWindow의 공격 간격 표시와 같은 이유)
    private const string INTERVAL_VALUE_LOC_KEY = "buildMode_panel_tower_interval_value";

    // 타워별 설명 키는 TowerData 에셋 이름에서 만든다(TD_CrossBow → buildMode_panel_tower_info_crossBow).
    private const string INFO_LOC_KEY_PREFIX = "buildMode_panel_tower_info_";
    private const string TOWER_DATA_ASSET_PREFIX = "TD_";

    [Header("Stat_tower")]
    [Tooltip("hp/Text (TMP) - 최대 체력(TowerData.MaxHealth).")]
    [SerializeField] private TMP_Text _healthText;

    [Tooltip("attack/Text (TMP) - 공격력(AttackSO의 피해 효과 합).")]
    [SerializeField] private TMP_Text _attackText;

    [Tooltip("attack-Interval/Text (TMP) - 공격 간격(AttackSO.Interval).")]
    [FormerlySerializedAs("_rangeText")]
    [SerializeField] private TMP_Text _attackIntervalText;

    [Tooltip("attack 행 전체. 공격 데이터가 없는 타워에서는 통째로 숨긴다.")]
    [SerializeField] private GameObject _attackRow;

    [Tooltip("attack-Interval 행 전체. 공격 데이터가 없는 타워에서는 통째로 숨긴다.")]
    [FormerlySerializedAs("_rangeRow")]
    [SerializeField] private GameObject _attackIntervalRow;

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

    // 공격 데이터가 없는 타워(TD_TimeTower 등)는 공격력·공격 간격 행을 통째로 숨긴다 -
    // 0을 보여주면 "공격력 0인 타워"로 읽혀 잘못된 정보가 된다.
    private void RenderAttack(AttackSO attack)
    {
        bool hasAttack = attack != null;

        if (_attackRow != null)
        {
            _attackRow.SetActive(hasAttack);
        }

        if (_attackIntervalRow != null)
        {
            _attackIntervalRow.SetActive(hasAttack);
        }

        if (!hasAttack)
        {
            return;
        }

        if (_attackText != null)
        {
            // 연구·용 스킬 보정은 반영하지 않는다(Neutral) - 이 창은 타워 자체의 기본 수치를 보여준다.
            // 배치된 타워의 실효 피해량은 TowerAttack.TryGetEffectiveDamage가 따로 돌려준다.
            attack.TryGetTotalDamage(ResolvedEnemyStatModifier.Neutral, out float damage);
            _attackText.text = Mathf.RoundToInt(damage).ToString();
        }

        if (_attackIntervalText != null)
        {
            // 배치된 타워의 실효 간격(지형·충원율·연구 보정 포함)은 TowerAttack이 따로 돌려준다 -
            // 여기는 건설 전 목록이므로 데이터 원본 값을 그대로 보여준다(공격력과 같은 기준).
            _attackIntervalText.text = AttackIntervalValue(attack.Interval);
        }
    }

    private static string AttackIntervalValue(float interval) =>
        string.Format(StringTable.GetString(INTERVAL_VALUE_LOC_KEY), interval);

    // 에셋 이름을 바꾸면 키가 어긋나지만, 그때는 StringTable이 키 문자열을 그대로 돌려주므로
    // 설명 자리에 "buildMode_panel_tower_info_..."가 그대로 보여 화면에서 바로 드러난다.
    private static string InfoLocKeyOf(TowerData data)
    {
        return LocKeySuffix.Build(INFO_LOC_KEY_PREFIX, data.name, TOWER_DATA_ASSET_PREFIX);
    }
}
