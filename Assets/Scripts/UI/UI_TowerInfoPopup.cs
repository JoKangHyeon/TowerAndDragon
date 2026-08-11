using TMPro;
using UnityEngine;

// 빌드모드 타워 슬롯에 마우스를 올렸을 때 슬롯 오른쪽에 뜨는 정보 창(Popup_Tower_Info).
// 스스로 뜨고 지는 시점을 정하지 않는다 - UI_BuildModeWindow가 호버를 받아 Show/Hide를 호출한다.
//
// 창을 띄울 때마다 내용을 새로 그리므로 언어 변경 구독은 두지 않는다(다음에 열릴 때 새 언어로 그려진다).
public class UI_TowerInfoPopup : MonoBehaviour
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

    [Tooltip("슬롯 오른쪽 끝에서 팝업까지 띄울 간격(캔버스 단위).")]
    [SerializeField] private float _slotGap = 8f;

    // 슬롯의 화면상 네 모서리를 받는 재사용 버퍼 - 호버할 때마다 배열을 새로 만들지 않는다.
    private readonly Vector3[] _slotCorners = new Vector3[4];

    private RectTransform _rect;
    private bool _isShown;

    private void Awake()
    {
        _rect = (RectTransform)transform;

        // 인스턴스가 활성으로 저장돼 있어도 시작 상태는 닫힘으로 맞춘다.
        //
        // _isShown 가드가 필요한 이유(CLAUDE.md 이벤트 초기화 규칙): 인스턴스가 비활성으로 저장돼 있으면
        // Unity는 첫 SetActive(true) 안에서 이 Awake를 동기 실행한다. 그때 무조건 닫으면 방금 연 팝업을
        // 스스로 닫아 첫 호버가 먹지 않는다. Show()가 SetActive 이전에 세우는 이 플래그로 두 경우를 가른다.
        if (!_isShown)
        {
            Hide();
        }
    }

    /// <summary>타워 정보를 채우고 슬롯 오른쪽에 띄운다.</summary>
    public void Show(TowerData data, RectTransform slotRect)
    {
        if (data == null || slotRect == null)
        {
            Hide();
            return;
        }

        Render(data);

        _isShown = true;              // SetActive 이전에 세운다
        gameObject.SetActive(true);   // 첫 활성화라면 이 안에서 Awake가 돈다

        // 위치는 활성화 뒤에 잡는다 - 꺼져 있는 동안에는 레이아웃이 갱신되지 않아 크기가 어긋난다.
        PlaceRightOf(slotRect);
    }

    public void Hide()
    {
        _isShown = false;
        gameObject.SetActive(false);
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
        string assetName = data.name;

        if (assetName.StartsWith(TOWER_DATA_ASSET_PREFIX))
        {
            assetName = assetName.Substring(TOWER_DATA_ASSET_PREFIX.Length);
        }

        if (assetName.Length == 0)
        {
            return string.Empty;
        }

        return INFO_LOC_KEY_PREFIX + char.ToLowerInvariant(assetName[0]) + assetName.Substring(1);
    }

    // 슬롯의 오른쪽 변 한가운데에 팝업의 피벗(좌측 중앙)을 붙인다.
    // 화면 좌표가 아니라 월드 좌표로 맞춘 뒤 간격만 로컬 단위로 더한다 -
    // 캔버스 스케일이 1이 아니어도 두 단위가 섞이지 않는다.
    private void PlaceRightOf(RectTransform slotRect)
    {
        if (_rect == null)
        {
            return;
        }

        slotRect.GetWorldCorners(_slotCorners);

        // GetWorldCorners: 0=좌하, 1=좌상, 2=우상, 3=우하.
        Vector3 rightEdgeCenter = (_slotCorners[2] + _slotCorners[3]) * 0.5f;

        _rect.position = rightEdgeCenter;
        _rect.anchoredPosition += new Vector2(_slotGap, 0f);
    }
}
