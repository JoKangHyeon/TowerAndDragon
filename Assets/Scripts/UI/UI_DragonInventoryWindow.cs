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
    private const string TITLE_LOC_KEY = "baby_dragon_inventory_title";

    private static readonly DragonType[] DRAGON_TYPES_IN_ORDER =
        (DragonType[])Enum.GetValues(typeof(DragonType));

    [Header("Dependencies")]
    [SerializeField] private GameManager _gameManager;
    [SerializeField] private BabyDragonDataCatalog _dataCatalog;
    [SerializeField] private BabyDragonPlacementCoordinator _placementCoordinator;
    [SerializeField] private UIManager _uiManager;
    [SerializeField] private CycleManager _cycleManager;

    [Header("패널")]
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;
    [Tooltip("InputActionReference — 보통 ESC.")]
    [SerializeField] private InputActionReference _closeAction;

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

        if (_openButton != null)
        {
            _openButton.onClick.AddListener(TogglePanel);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(ClosePanel);
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

    private void Update()
    {
        if (!_isOpen)
        {
            return;
        }

        if (_closeAction != null && _closeAction.action.WasPerformedThisFrame())
        {
            ClosePanel();
        }
    }

    private void HandleNightStart(int _)
    {
        if (_isOpen)
        {
            ClosePanel();
        }
    }

    private void TogglePanel()
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

        foreach (BabyDragon dragon in run.BabyDragons)
        {
            if (!_dataCatalog.TryResolve(dragon.DragonType, out BabyDragonData data))
            {
                continue;
            }

            UI_DragonInventorySlot slot = Instantiate(_slotPrefab, _slotContainer);
            slot.SetupDragon(dragon, data, ColorForType(dragon.DragonType), OnSlotPlaceClicked);
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
