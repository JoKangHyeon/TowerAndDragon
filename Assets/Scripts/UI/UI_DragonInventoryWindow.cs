using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 새끼용 인벤토리 창. 알·용을 슬롯으로 나열하고, 용 클릭 시 그리드 배치를 시작한다.
/// IExclusiveMode를 구현해 빌드모드·점령 등 배타 UI와 상호 배제한다.
/// RunData.OnInventoryChanged 구독으로 알 획득·부화·배치·철거 이벤트를 자동 반영한다.
/// </summary>
public class UI_DragonInventoryWindow : MonoBehaviour, IExclusiveMode
{
    // 탭 필터 - 알 목록 / 부화한 용 목록 중 하나만 슬롯 컨테이너에 채운다.
    private enum InventoryTab
    {
        Egg,
        Dragon,
    }

    // 탭 버튼 하나 - 선택되면 MenuFocus가 활성, 아니면 MenuDefault가 활성이 된다(UI_BuildModeWindow.FilterTab과 동일한 패턴).
    [Serializable]
    private struct FilterTab
    {
        public Button Button;
        public GameObject MenuFocus;
        public GameObject MenuDefault;
    }

    private const string TITLE_LOC_KEY = "baby_dragon_inventory_title";

    private static readonly DragonType[] DRAGON_TYPES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    [Header("Dependencies")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private BabyDragonDataCatalog _dataCatalog;
    [SerializeField] private BabyDragonPlacementCoordinator _placementCoordinator;
    [SerializeField] private BuildingPlacementController _buildingPlacementController;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private UIManager _uiManager;

    [Header("패널")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _titleText;
    [Tooltip("InputActionReference — 보통 ESC.")]
    [SerializeField] private InputActionReference _closeAction;

    [Header("탭 (알 / 용)")]
    [SerializeField] private FilterTab _eggTab;
    [SerializeField] private FilterTab _dragonTab;

    [Header("이동/철거 버튼 - 그리드에서 선택된 새끼용 대상")]
    [SerializeField] private Button _moveButton;
    [SerializeField] private Button _removeButton;

    [Header("슬롯")]
    [SerializeField] private Transform _slotContainer;
    [SerializeField] private UI_DragonInventorySlot _slotPrefab;
    [Tooltip("공용 알 스프라이트 (속성 색으로 틴트됨).")]
    [SerializeField] private Sprite _eggSprite;

    [Header("속성 색상 (DragonType 선언 순: Ice, Fire, Time, Stone, Life)")]
    [SerializeField]
    private Color[] _attributeColors =
    {
        new Color(0.357f, 0.753f, 0.878f), // Ice
        new Color(0.878f, 0.376f, 0.235f), // Fire
        new Color(0.690f, 0.490f, 0.878f), // Time
        new Color(0.788f, 0.635f, 0.290f), // Stone
        new Color(0.498f, 0.820f, 0.310f), // Life
    };

    [Header("패널 슬라이드 연출")]
    [SerializeField] private float _slideDuration = 0.5f;
    [Tooltip("열릴 때 시작 오프셋(홈 기준). 여기서 홈으로 슬라이드 인.")]
    [SerializeField] private Vector2 _openFromOffset = new Vector2(-50f, 0f);
    [Tooltip("닫힐 때 도착 오프셋(홈 기준). 홈에서 여기로 슬라이드 아웃 후 비활성화.")]
    [SerializeField] private Vector2 _closeToOffset = new Vector2(-500f, 0f);

    private RectTransform _panelRect;
    private Vector2 _homePos;
    private bool _isOpen;
    private Tween _panelTween;
    private InventoryTab _currentTab = InventoryTab.Egg;
    private readonly List<UI_DragonInventorySlot> _spawnedSlots = new();

    private void Awake()
    {
        _panelRect = _panel.GetComponent<RectTransform>();
        _homePos = _panelRect.anchoredPosition;
        _panel.SetActive(false);

        if (_titleText != null)
        {
            _titleText.text = StringTable.GetString(TITLE_LOC_KEY);
        }

        if (_eggTab.Button != null)
        {
            _eggTab.Button.onClick.AddListener(() => SelectTab(InventoryTab.Egg));
        }

        if (_dragonTab.Button != null)
        {
            _dragonTab.Button.onClick.AddListener(() => SelectTab(InventoryTab.Dragon));
        }

        SelectTab(_currentTab);

        if (_moveButton != null && _buildingPlacementController != null)
        {
            _moveButton.onClick.AddListener(() => _buildingPlacementController.EnterMoveMode());
        }

        if (_removeButton != null && _buildingPlacementController != null)
        {
            // 새끼용도 Building을 상속하므로 별도 로직 없이 그리드 제거 흐름을 그대로 탄다 -
            // BabyDragonPlacementCoordinator.HandleBuildingRemoving이 OnBuildingRemoving을 구독해
            // 제거된 용을 자동으로 인벤토리(IsInTower=false)로 되돌린다.
            _removeButton.onClick.AddListener(() => _buildingPlacementController.RemoveSelectedBuilding());
        }

        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.AddListener(HandleNightStart);
        }

        if (_gameManager != null && _gameManager.CurrentRun != null)
        {
            _gameManager.CurrentRun.OnInventoryChanged.AddListener(RebuildSlots);
        }
    }

    private void OnDestroy()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
        }

        if (_gameManager != null && _gameManager.CurrentRun != null)
        {
            _gameManager.CurrentRun.OnInventoryChanged.RemoveListener(RebuildSlots);
        }

        if (_panelTween != null)
        {
            _panelTween.Kill();
        }
    }

    // BuildMode와 같은 패턴: _panel이 이 스크립트가 붙은 오브젝트 자신이라, 패널이 닫히면
    // 이 오브젝트도 비활성화되어 Update()가 멈춘다 - 재오픈은 항상 외부(UIManager의 단축키 폴링)가
    // IExclusiveMode.Open()을 직접 호출해서 이뤄진다. ESC로 닫는 것만 여기서 스스로 처리하면 된다.
    private void Update()
    {
        SyncActiveButtons();

        if (!_isOpen)
        {
            return;
        }

        if (_closeAction != null && _closeAction.action.WasPerformedThisFrame())
        {
            ClosePanel();
        }
    }

    // 그리드에서 선택된 건물 기준으로 이동/철거 버튼 상태를 매 프레임 동기화한다(UI_BuildModeWindow.Update와 동일).
    private void SyncActiveButtons()
    {
        if (_buildingPlacementController == null)
        {
            return;
        }

        Building selected = _buildingPlacementController.SelectedBuilding;

        if (_moveButton != null)
        {
            _moveButton.gameObject.SetActive(!_buildingPlacementController.IsMoving);
            _moveButton.interactable = selected != null && selected.IsMoveable;
        }

        if (_removeButton != null)
        {
            _removeButton.interactable = selected != null && selected.IsRemoveable;
        }
    }

    private void HandleNightStart(int _)
    {
        if (_isOpen)
        {
            ClosePanel();
        }
    }

    // 선택된 탭만 Focus 상태로, 나머지는 Default 상태로 만들고, 슬롯 목록을 그 탭 기준으로 다시 그린다.
    private void SelectTab(InventoryTab tab)
    {
        _currentTab = tab;

        if (_eggTab.MenuFocus != null)
        {
            _eggTab.MenuFocus.SetActive(tab == InventoryTab.Egg);
        }

        if (_eggTab.MenuDefault != null)
        {
            _eggTab.MenuDefault.SetActive(tab != InventoryTab.Egg);
        }

        if (_dragonTab.MenuFocus != null)
        {
            _dragonTab.MenuFocus.SetActive(tab == InventoryTab.Dragon);
        }

        if (_dragonTab.MenuDefault != null)
        {
            _dragonTab.MenuDefault.SetActive(tab != InventoryTab.Dragon);
        }

        RebuildSlots();
    }

    private void OpenPanel()
    {
        _isOpen = true;
        _panelTween?.Kill();

        _panel.SetActive(true);
        _panelRect.anchoredPosition = _homePos + _openFromOffset;
        _panelTween = _panelRect.DOAnchorPos(_homePos, _slideDuration)
            .SetEase(Ease.OutBack)
            .SetLink(_panel);

        RebuildSlots();
    }

    private void ClosePanel()
    {
        _isOpen = false;
        _panelTween?.Kill();
        _panelTween = _panelRect.DOAnchorPos(_homePos + _closeToOffset, _slideDuration)
            .SetEase(Ease.InCubic)
            .SetLink(_panel)
            .OnComplete(() => _panel.SetActive(false));
    }

    private void RebuildSlots()
    {
        if (!_isOpen)
        {
            return;
        }

        foreach (UI_DragonInventorySlot slot in _spawnedSlots)
        {
            if (slot != null)
            {
                Destroy(slot.gameObject);
            }
        }
        _spawnedSlots.Clear();

        if (_slotPrefab == null || _slotContainer == null || _gameManager == null)
        {
            return;
        }

        RunData run = _gameManager.CurrentRun;

        if (_currentTab == InventoryTab.Egg)
        {
            BuildEggSlots(run);
        }
        else
        {
            BuildDragonSlots(run);
        }
    }

    private void BuildEggSlots(RunData run)
    {
        foreach (DragonEgg egg in run.DragonEggs)
        {
            if (!_dataCatalog.TryResolve(egg.DragonType, out BabyDragonData data))
            {
                continue;
            }

            UI_DragonInventorySlot slot = Instantiate(_slotPrefab, _slotContainer);
            slot.SetupEgg(egg, data, _eggSprite, ColorForType(egg.DragonType));
            _spawnedSlots.Add(slot);
        }
    }

    private void BuildDragonSlots(RunData run)
    {
        // 미설치 용의 "지금 설치하면 하루에 얼마 먹을지" 미리보기에 쓸, 현재 설치된 개체 집계.
        // BabyDragonFeedingSystem이 쓰는 집계와 같은 GridMap 이벤트에 맞춰 갱신되므로 항상 일치한다.
        var installedCountByType = new Dictionary<DragonType, int>();
        int installedTotal = 0;

        foreach (BabyDragon installed in run.BabyDragons)
        {
            if (!installed.IsInTower)
            {
                continue;
            }

            installedCountByType.TryGetValue(installed.DragonType, out int count);
            installedCountByType[installed.DragonType] = count + 1;
            installedTotal++;
        }

        foreach (BabyDragon dragon in run.BabyDragons)
        {
            // 설치 중인 용은 인벤토리에 표시하지 않는다 - 리스트 자체는 보유 여부 판정(KinOwnedGateSO 등)에
            // 쓰이므로 지우지 않고 여기서만 걸러낸다.
            if (dragon.IsInTower)
            {
                continue;
            }

            if (!_dataCatalog.TryResolve(dragon.DragonType, out BabyDragonData data))
            {
                continue;
            }

            installedCountByType.TryGetValue(dragon.DragonType, out int sameTypeCount);
            int previewDailyFeed = BabyDragonFeedFormula.ResolveDailyFeed(data, sameTypeCount + 1, installedTotal + 1);

            UI_DragonInventorySlot slot = Instantiate(_slotPrefab, _slotContainer);
            slot.SetupDragon(dragon, data, ColorForType(dragon.DragonType), previewDailyFeed, OnSlotPlaceClicked);
            _spawnedSlots.Add(slot);
        }
    }

    private void OnSlotPlaceClicked(BabyDragon dragon)
    {
        if (_placementCoordinator != null)
        {
            _placementCoordinator.BeginPlacement(dragon);
        }
    }

    // 인게임 HUD(UI_IngameWindow)의 새끼용 인벤토리 버튼이 호출하는 진입점 - UI_ResearchWindow/
    // UI_DragonSkillWindow.ToggleFromEntryPoint와 동일한 패턴.
    public void ToggleFromEntryPoint()
    {
        if (_isOpen)
        {
            ClosePanel();
        }
        else if (_uiManager != null)
        {
            _uiManager.OpenExclusive(this);
        }
        else
        {
            OpenPanel();
        }
    }

    bool IExclusiveMode.IsOpen => _isOpen;
    void IExclusiveMode.Open() => OpenPanel();
    void IExclusiveMode.Close()
    {
        if (_isOpen)
        {
            ClosePanel();
        }
    }

    private Color ColorForType(DragonType type)
    {
        int index = Array.IndexOf(DRAGON_TYPES_IN_ORDER, type);
        return index >= 0 && index < _attributeColors.Length ? _attributeColors[index] : Color.white;
    }
}
