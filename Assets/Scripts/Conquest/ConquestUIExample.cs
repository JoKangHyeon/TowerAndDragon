using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

// 점령 모드 버튼 + 점령 정보 패널. 건설 모드 창(UI_BuildModeWindow)과 동일한 토글/슬라이드 패턴을 따른다.
// 기간/지형 슬롯은 모든 청크에 항상 존재하므로 씬에 미리 고정 배치해 두고 값만 갱신한다(스폰/삭제 없음).
// 반면 자원 비용과 몬스터 강화 효과(스폰배율/공격배율)는 청크마다 실제로 적용되는 항목이 달라서,
// 해당하는 항목만 UI_ResourceCostSlot / UI_ConquestInfoSlot을 매번 새로 생성한다(건물 슬롯과 동일한 패턴).
public class ConquestUIExample : MonoBehaviour
{
    private const string HELD_OVER_REQUIRED_FORMAT = "{0}/{1}";
    private const string PLUS_VALUE_FORMAT = "+{0}";

    // TODO: 스트링테이블 도입 시 아래 5개를 _LOC_KEY로 교체. DAY_SINGULAR/PLURAL_FORMAT의
    // 단/복수 분기(FormatDuration)도 언어별 규칙이 다를 수 있어 그때 같이 재검토 필요.
    private const string DURATION_LABEL = "Duration";
    private const string YIELD_LABEL = "Yield";
    private const string SPAWN_LABEL = "Spawn";
    private const string ATTACK_LABEL = "Attack";
    private const string DURATION_DAY_SINGULAR_FORMAT = "{0} Day";
    private const string DURATION_DAY_PLURAL_FORMAT = "{0} Days";

    [SerializeField]
    private Button _conquestModeButton;

    [SerializeField]
    private Button _conquerButton;

    [SerializeField]
    private GameObject _conquestModePanel;

    [Tooltip("자원 비용 슬롯 프리팹(ResourceCost).")]
    [SerializeField]
    private UI_ResourceCostSlot _resourceCostSlotPrefab;

    [Tooltip("생성된 자원 비용 슬롯이 들어갈 부모.")]
    [SerializeField]
    private Transform _resourceSlotContainer;

    [Tooltip("자원 아이콘. 순서는 인구/식량/통나무/돌/불꽃의 심장/눈의 결정/시간의 모래/현자의 돌 (인구 + ResourceType 선언 순서).")]
    [SerializeField]
    private Sprite[] _resourceIcons;

    private readonly List<UI_ResourceCostSlot> _spawnedResourceSlots = new();

    [Header("보상(해금 자원 종류) 슬롯 - 실제 지급 수량이 아닌 해금 표시용. 인구 보상은 별도로 즉시 지급된다.")]
    [Tooltip("보상 슬롯 프리팹(Slot_ConquestReward).")]
    [SerializeField]
    private UI_ConquestRewardSlot _rewardSlotPrefab;

    [Tooltip("생성된 보상 슬롯이 들어갈 부모.")]
    [SerializeField]
    private Transform _rewardSlotContainer;

    [Tooltip("생산량 보상 슬롯 아이콘 - 특정 자원이 아니라 청크의 기본 생산량(Chunk.BaseYield)을 가리키므로 별도 아이콘을 쓴다.")]
    [SerializeField]
    private Sprite _yieldIcon;

    // _resourceIcons를 그대로 재사용 - 인덱스 1부터가 이 배열 순서와 대응(_resourceIcons[0]은 인구).
    private static readonly ResourceType[] REWARD_RESOURCE_TYPES =
    {
        ResourceType.Food,
        ResourceType.Wood,
        ResourceType.Stone,
        ResourceType.FlameHeart,
        ResourceType.SnowCrystal,
        ResourceType.TimeSand,
        ResourceType.PhilosopherStone,
    };

    private readonly List<UI_ConquestRewardSlot> _spawnedRewardSlots = new();

    [Header("고정 정보 슬롯 (기간/지형) - 씬에 미리 배치된 인스턴스")]
    [Tooltip("소요 기간 슬롯(ConquestInfoImage) - 아이콘 + \"N Day(s)\" 텍스트.")]
    [SerializeField]
    private UI_ConquestInfoSlot _durationInfoSlot;

    [SerializeField]
    private Sprite _durationIcon;

    [Tooltip("지형 아이콘 이미지(TerrainImage) - 텍스트 없이 지형에 맞는 스프라이트만 교체한다.")]
    [SerializeField]
    private Image _terrainImage;

    [Tooltip("지형 스프라이트. 인덱스는 TerrainType 선언 순서(Grass/Rock/Volcano/Desert/Snow/Default)와 일치해야 한다.")]
    [SerializeField]
    private Sprite[] _terrainSprites;

    [Header("몬스터 강화 효과 슬롯 - 적용되는 항목만 생성")]
    [Tooltip("몬스터 강화 효과 슬롯 프리팹(ConquestInfoImage).")]
    [SerializeField]
    private UI_ConquestInfoSlot _enemyScalingSlotPrefab;

    [Tooltip("생성된 몬스터 강화 효과 슬롯이 들어갈 부모.")]
    [SerializeField]
    private Transform _enemyScalingSlotContainer;

    [SerializeField]
    private Sprite _spawnCountIcon;

    [SerializeField]
    private Sprite _attackPowerIcon;

    private readonly List<UI_ConquestInfoSlot> _spawnedEnemyScalingSlots = new();

    private static readonly Color SUFFICIENT_COLOR_DEFAULT = new Color(0f, 0.6f, 0f);

    [SerializeField]
    private Color _sufficientColor = SUFFICIENT_COLOR_DEFAULT;

    [SerializeField]
    private Color _insufficientColor = Color.red;

    [SerializeField]
    private ConquestManager _conquestManager;

    [Tooltip("자원 보유량 관리자.")]
    [SerializeField]
    private ResourceManager _resourceManager;

    [Tooltip("인구 보유량 관리자 - 원정 인구 비용 충족 여부 확인용.")]
    [SerializeField]
    private PopulationManager _populationManager;

    [SerializeField]
    private ConquestModeController _conquestModeController;

    [Header("패널 열림/닫힘 연출")]
    [SerializeField]
    private float _slideDuration = 0.5f;

    [SerializeField]
    private Vector2 _openFromOffset = new Vector2(50f, 0f);

    [SerializeField]
    private Vector2 _closeToOffset = new Vector2(500f, 0f);

    private RectTransform _panelRect;
    private Vector2 _homePos;
    private Tween _panelTween;
    private Vector2Int? _selectedChunkCoord;

    private void Awake()
    {
        _conquestModeButton.onClick.AddListener(ToggleConquestMode);
        _conquerButton.onClick.AddListener(OnConquerButtonClicked);

        _panelRect = _conquestModePanel.GetComponent<RectTransform>();
        _homePos = _panelRect.anchoredPosition;

        _conquestModePanel.SetActive(false);
    }

    private void OnEnable()
    {
        _conquestManager.OnConquestCompleted.AddListener(OnConquestCompleted);
    }

    // 패널이 열려 있는 상태에서 점령 모드가 꺼지면 ConquestModeController.SetConquestModeActive(false)가
    // 알아서 패널을 닫는다(ESC 처리도 그쪽 HandleCancelInput이 담당) - 여기서 중복 구현하지 않는다.
    private void OnDisable()
    {
        _conquestManager.OnConquestCompleted.RemoveListener(OnConquestCompleted);
    }

    // 원정이 실제로 완료된 시점(며칠 뒤 밤 정산)에 ConquestManager가 발행한다.
    // 인구 보상 지급과 원정 인구 반환은 ConquestPopulationCoordinator가 같은 이벤트를 구독해 처리한다.
    private void OnConquestCompleted(Vector2Int chunkCoord)
    {
        _conquestModeController.RefreshConquerableHighlights();
    }

    private void ToggleConquestMode()
    {
        _conquestModeController.SetConquestModeActive(!_conquestModeController.IsActive);
    }

    // ConquestModeController가 점령 가능한 청크를 클릭했을 때 호출하는 진입점.
    public void OnChunkSelected(Vector2Int chunkCoord)
    {
        _selectedChunkCoord = chunkCoord;
        _conquestModeController.LockChunkSelection(chunkCoord);
        OpenPanel();
        Refresh();
    }

    // 씬에 미리 세팅해 둔 위치(_homePos)에서 슬라이드 인 시킨다.
    private void OpenPanel()
    {
        _panelTween?.Kill();

        _conquestModePanel.SetActive(true);
        _panelRect.anchoredPosition = _homePos + _openFromOffset;
        _panelTween = _panelRect.DOAnchorPos(_homePos, _slideDuration)
            .SetEase(Ease.OutBack)
            .SetLink(_conquestModePanel);
    }

    // 패널만 닫는다 - 점령 모드 자체는 유지되어 이어서 다른 청크를 선택할 수 있다.
    // 청크 하이라이트(노란색 포함)는 지우지 않는다 - 이제 호버로 계속 갱신되고, 점령 모드가 꺼질 때까지 유지된다.
    public void Close()
    {
        _selectedChunkCoord = null;
        _conquestModeController.UnlockChunkSelection();

        _panelTween?.Kill();
        _panelTween = _panelRect.DOAnchorPos(_homePos + _closeToOffset, _slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(_conquestModePanel)
            .OnComplete(() => _conquestModePanel.SetActive(false));
    }

    private void Refresh()
    {
        if (!_selectedChunkCoord.HasValue)
        {
            return;
        }

        Vector2Int coord = _selectedChunkCoord.Value;
        bool hasCost = _conquestManager.TryGetExpeditionCost(coord, out ResourceCost cost);

        ResourceCost held = _resourceManager.GetHoldingsSnapshot();
        held.Population = _populationManager != null ? _populationManager.AvailablePopulation : cost.Population;

        RebuildResourceSlots(held, cost);
        RebuildRewardSlots(
            _conquestManager.GetPopulationReward(coord),
            _conquestManager.GetUnlockedResources(coord),
            _conquestManager.GetChunkYield(coord));

        if (_durationInfoSlot != null)
        {
            int daysRequired = _conquestManager.GetDaysRequired(coord);
            _durationInfoSlot.Setup(_durationIcon, DURATION_LABEL, FormatDuration(daysRequired));
        }

        if (_terrainImage != null)
        {
            TerrainType terrain = _conquestManager.GetDominantTerrain(coord);
            Sprite terrainSprite = ResolveTerrainSprite(terrain);
            if (terrainSprite != null)
                _terrainImage.sprite = terrainSprite;
        }

        EnemyScalingModifier scaling = _conquestManager.PreviewEnemyScaling(coord);
        RebuildEnemyScalingSlots(scaling);

        bool canSend = _conquestManager.CanSendExpedition(coord);
        bool canAfford = _conquestManager.CanAffordExpedition(coord, held);
        _conquerButton.interactable = hasCost && canSend && canAfford;
    }

    // 1일이면 단수(Day), 2일 이상이면 복수(Days) 표기.
    private static string FormatDuration(int days) =>
        string.Format(days == 1 ? DURATION_DAY_SINGULAR_FORMAT : DURATION_DAY_PLURAL_FORMAT, days);

    private Sprite ResolveTerrainSprite(TerrainType terrain)
    {
        int index = (int)terrain;
        return _terrainSprites != null && index < _terrainSprites.Length ? _terrainSprites[index] : null;
    }

    // 이전에 생성된 자원 슬롯을 지우고, 이번 청크가 실제로 요구하는 자원(요구량 > 0)만큼만 새로 생성한다.
    private void RebuildResourceSlots(ResourceCost held, ResourceCost cost)
    {
        foreach (UI_ResourceCostSlot slot in _spawnedResourceSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        _spawnedResourceSlots.Clear();

        if (_resourceCostSlotPrefab == null || _resourceSlotContainer == null)
            return;

        // _resourceIcons[0]은 인구 - held/cost 배열도 같은 순서로 맞춰 인덱스를 그대로 아이콘 인덱스로 쓴다.
        int[] heldValues = { held.Population, held.Food, held.Wood, held.Stone };
        int[] requiredValues = { cost.Population, cost.Food, cost.Wood, cost.Stone };

        for (int i = 0; i < requiredValues.Length; i++)
        {
            if (requiredValues[i] <= 0)
                continue;

            Sprite icon = _resourceIcons != null && i < _resourceIcons.Length ? _resourceIcons[i] : null;
            Color textColor = heldValues[i] < requiredValues[i] ? _insufficientColor : _sufficientColor;
            string countText = string.Format(HELD_OVER_REQUIRED_FORMAT, heldValues[i], requiredValues[i]);

            UI_ResourceCostSlot slot = Instantiate(_resourceCostSlotPrefab, _resourceSlotContainer);
            slot.Setup(icon, countText, textColor);
            _spawnedResourceSlots.Add(slot);
        }
    }

    // 이전에 생성된 보상 슬롯을 지우고 다시 생성한다.
    // 인구/생산량은 실제 수치(populationReward, yield > 0)를, 나머지 자원은 해금된 종류(unlockedResources)만 표시한다.
    private void RebuildRewardSlots(int populationReward, ResourceType unlockedResources, int yield)
    {
        foreach (UI_ConquestRewardSlot slot in _spawnedRewardSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        _spawnedRewardSlots.Clear();

        if (_rewardSlotPrefab == null || _rewardSlotContainer == null)
            return;

        if (populationReward > 0)
        {
            Sprite populationIcon = _resourceIcons != null && _resourceIcons.Length > 0 ? _resourceIcons[0] : null;
            SpawnRewardSlot(populationIcon, string.Format(PLUS_VALUE_FORMAT, populationReward));
        }

        if (yield > 0)
        {
            SpawnRewardSlot(_yieldIcon, string.Format(PLUS_VALUE_FORMAT, yield));
        }

        for (int i = 0; i < REWARD_RESOURCE_TYPES.Length; i++)
        {
            ResourceType type = REWARD_RESOURCE_TYPES[i];
            if ((unlockedResources & type) == 0)
                continue;

            int iconIndex = i + 1; // _resourceIcons[0]은 인구
            Sprite icon = _resourceIcons != null && iconIndex < _resourceIcons.Length ? _resourceIcons[iconIndex] : null;
            SpawnRewardSlot(icon, type.ToString());
        }
    }

    private void SpawnRewardSlot(Sprite icon, string label)
    {
        UI_ConquestRewardSlot slot = Instantiate(_rewardSlotPrefab, _rewardSlotContainer);
        slot.Setup(icon, label);
        _spawnedRewardSlots.Add(slot);
    }

    // 이전에 생성된 몬스터 강화 효과 슬롯을 지우고, 이번 청크가 실제로 강화하는 항목(스폰 수/공격력)만 새로 생성한다.
    private void RebuildEnemyScalingSlots(EnemyScalingModifier scaling)
    {
        foreach (UI_ConquestInfoSlot slot in _spawnedEnemyScalingSlots)
        {
            if (slot != null)
                Destroy(slot.gameObject);
        }
        _spawnedEnemyScalingSlots.Clear();

        if (_enemyScalingSlotPrefab == null || _enemyScalingSlotContainer == null)
            return;

        if (scaling.AffectsSpawnCount)
            SpawnEnemyScalingSlot(_spawnCountIcon, SPAWN_LABEL, string.Format(PLUS_VALUE_FORMAT, scaling.SpawnCountBonus));

        if (scaling.AffectsAttackPower)
            SpawnEnemyScalingSlot(_attackPowerIcon, ATTACK_LABEL, string.Format(PLUS_VALUE_FORMAT, scaling.AttackPowerBonus));
    }

    private void SpawnEnemyScalingSlot(Sprite icon, string label, string valueText)
    {
        UI_ConquestInfoSlot slot = Instantiate(_enemyScalingSlotPrefab, _enemyScalingSlotContainer);
        slot.Setup(icon, label, valueText);
        _spawnedEnemyScalingSlots.Add(slot);
    }

    private void OnConquerButtonClicked()
    {
        if (!_selectedChunkCoord.HasValue)
        {
            return;
        }

        Vector2Int coord = _selectedChunkCoord.Value;
        bool hasCost = _conquestManager.TryGetExpeditionCost(coord, out ResourceCost cost);

        ResourceCost held = _resourceManager.GetHoldingsSnapshot();
        held.Population = _populationManager != null ? _populationManager.AvailablePopulation : cost.Population;

        bool sent = _conquestManager.SendExpedition(coord, held);
        if (sent)
        {
            if (hasCost)
                _resourceManager.Spend(cost); // 자원 차감(원정 발송 시점) - 인구 배치/반환/보상은 ConquestPopulationCoordinator가 처리

            _conquestModeController.RefreshConquerableHighlights(); // 원정 중인 청크는 CanSendExpedition이 false가 되므로 즉시 갱신
        }

        Close();
    }
}
