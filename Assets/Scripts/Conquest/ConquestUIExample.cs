using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

// 점령 모드 버튼 + 점령 정보 패널. 건설 모드 창(UI_BuildModeWindow)과 동일한 토글/슬라이드 패턴을 따른다.
// 기간/지형 슬롯은 모든 청크에 항상 존재하므로 씬에 미리 고정 배치해 두고 값만 갱신한다(스폰/삭제 없음).
// 반면 자원 비용과 몬스터 강화 효과(스폰배율/공격배율)는 청크마다 실제로 적용되는 항목이 달라서,
// 해당하는 항목만 UI_ResourceCostSlot / UI_ConquestInfoSlot을 매번 새로 생성한다(건물 슬롯과 동일한 패턴).
public class ConquestUIExample : MonoBehaviour
{
    private const string HELD_OVER_REQUIRED_FORMAT = "{0}/{1}";
    private const string MULTIPLIER_FORMAT = "0.00";
    private const string DURATION_LABEL = "Duration";
    private const string TERRAIN_LABEL = "Terrain";
    private const string SPAWN_MULTIPLIER_LABEL = "Spawn";
    private const string ATTACK_MULTIPLIER_LABEL = "Attack";

    [SerializeField]
    private Button _conquestModeButton;

    [SerializeField]
    private Button _conquerButton;

    [SerializeField]
    private GameObject _conquestModePanel;

    [SerializeField]
    private InputActionReference _cancelAction;

    [Tooltip("자원 비용 슬롯 프리팹(ResourceCost).")]
    [SerializeField]
    private UI_ResourceCostSlot _resourceCostSlotPrefab;

    [Tooltip("생성된 자원 비용 슬롯이 들어갈 부모.")]
    [SerializeField]
    private Transform _resourceSlotContainer;

    [Tooltip("자원 아이콘 5개. 순서는 인구/식량/나무/돌/광물(ResourceCost 필드 순서)과 일치해야 한다.")]
    [SerializeField]
    private Sprite[] _resourceIcons;

    private readonly List<UI_ResourceCostSlot> _spawnedResourceSlots = new();

    [Header("고정 정보 슬롯 (기간/지형) - 씬에 미리 배치된 ConquestInfoImage 인스턴스")]
    [SerializeField]
    private UI_ConquestInfoSlot _durationInfoSlot;

    [SerializeField]
    private Sprite _durationIcon;

    [SerializeField]
    private UI_ConquestInfoSlot _terrainInfoSlot;

    [SerializeField]
    private Sprite _terrainIcon;

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

    [SerializeField]
    private Color _sufficientColor = Color.green;

    [SerializeField]
    private Color _insufficientColor = Color.red;

    [SerializeField]
    private ConquestManager _conquestManager;

    [Tooltip("임시 - 자원/인구 매니저가 생기면 교체 예정.")]
    [SerializeField]
    private TempResourcePool _tempResourcePool;

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
        if (_cancelAction != null)
            _cancelAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_cancelAction != null)
            _cancelAction.action.Disable();
    }

    private void Update()
    {
        HandleCancelInput();
    }

    private void HandleCancelInput()
    {
        if (!_conquestModePanel.activeSelf)
            return;

        if (_cancelAction != null && _cancelAction.action.WasPerformedThisFrame())
            Close();
    }

    private void ToggleConquestMode()
    {
        bool nextActive = !_conquestModeController.IsActive;
        _conquestModeController.SetConquestModeActive(nextActive);

        if (!nextActive)
        {
            Close();
        }
    }

    // ConquestModeController가 점령 가능한 청크를 클릭했을 때 호출하는 진입점.
    public void OnChunkSelected(Vector2Int chunkCoord)
    {
        _selectedChunkCoord = chunkCoord;
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
        ResourceCost held = _tempResourcePool.Current;

        RebuildResourceSlots(held, cost);

        if (_durationInfoSlot != null)
        {
            int daysRequired = _conquestManager.GetDaysRequired(coord);
            _durationInfoSlot.Setup(_durationIcon, DURATION_LABEL, daysRequired.ToString());
        }

        if (_terrainInfoSlot != null)
        {
            string terrain = _conquestManager.GetDominantTerrain(coord).ToString();
            _terrainInfoSlot.Setup(_terrainIcon, TERRAIN_LABEL, terrain);
        }

        EnemyScalingModifier scaling = _conquestManager.PreviewEnemyScaling(coord);
        RebuildEnemyScalingSlots(scaling);

        bool canSend = _conquestManager.CanSendExpedition(coord);
        bool canAfford = _conquestManager.CanAffordExpedition(coord, held);
        _conquerButton.interactable = hasCost && canSend && canAfford;
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

        int[] heldValues = { held.Population, held.Food, held.Wood, held.Stone, held.Ore };
        int[] requiredValues = { cost.Population, cost.Food, cost.Wood, cost.Stone, cost.Ore };

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
            SpawnEnemyScalingSlot(_spawnCountIcon, SPAWN_MULTIPLIER_LABEL, scaling.SpawnCountMultiplier);

        if (scaling.AffectsAttackPower)
            SpawnEnemyScalingSlot(_attackPowerIcon, ATTACK_MULTIPLIER_LABEL, scaling.AttackPowermultiplier);
    }

    private void SpawnEnemyScalingSlot(Sprite icon, string label, float multiplier)
    {
        UI_ConquestInfoSlot slot = Instantiate(_enemyScalingSlotPrefab, _enemyScalingSlotContainer);
        slot.Setup(icon, label, multiplier.ToString(MULTIPLIER_FORMAT));
        _spawnedEnemyScalingSlots.Add(slot);
    }

    private void OnConquerButtonClicked()
    {
        if (!_selectedChunkCoord.HasValue)
        {
            return;
        }

        bool completed = _conquestManager.SendExpeditionAndComplete(_selectedChunkCoord.Value, _tempResourcePool.Current);
        if (completed)
        {
            _conquestModeController.RefreshConquerableHighlights();
        }

        Close();
    }
}
