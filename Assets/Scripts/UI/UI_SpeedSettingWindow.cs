using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤에 노출되는 속도 조절 UI(Speed_setting) 컨트롤러.
/// 일시정지/재생/배속 버튼을 GameSpeedManager에 연결하고, 현재 속도 상태를 하이라이트/라벨로 반영한다.
/// </summary>
public class UI_SpeedSettingWindow : MonoBehaviour
{
    // stringtable — game_speed_multiplier_format : "x{0}"
    private const string SPEED_MULTIPLIER_FORMAT_LOC_KEY = "game_speed_multiplier_format";

    [SerializeField] private GameSpeedManager _speedManager;

    [Header("버튼")]
    [SerializeField] private Button _buttonPause;
    [SerializeField] private Button _buttonPlay;
    [SerializeField] private Button _buttonSpeedUp;

    [Header("선택 표시(Select 프레임)")]
    [SerializeField] private GameObject _selectPause;
    [SerializeField] private GameObject _selectPlay;
    [SerializeField] private GameObject _selectSpeedUp;

    [Tooltip("배속 버튼의 배율 라벨(Text (TMP)). x2 / x3로 갱신된다.")]
    [SerializeField] private TMP_Text _speedUpLabel;

    /// <summary>
    /// 안내가 도는 동안 속도 조작을 막는 관문. 배선되지 않으면 null로 남아 늘 허용된다.
    /// </summary>
    public IHudControlBlockQuery BlockQuery { get; set; }

    private void Awake()
    {
        if (_buttonPause != null && _speedManager != null)
        {
            _buttonPause.onClick.AddListener(Pause);
        }

        if (_buttonPlay != null && _speedManager != null)
        {
            _buttonPlay.onClick.AddListener(ResumeNormal);
        }

        if (_buttonSpeedUp != null && _speedManager != null)
        {
            _buttonSpeedUp.onClick.AddListener(CycleFastForward);
        }
    }

    // GameSpeedManager를 그대로 물리지 않고 래핑하는 이유: 같은 메서드가 단축키 경로에서도
    // 호출되므로, 클릭음은 버튼을 물리는 이쪽에서만 내야 한다.
    private void Pause()
    {
        if (IsBlocked)
        {
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);
        _speedManager.Pause();
    }

    private void ResumeNormal()
    {
        if (IsBlocked)
        {
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);
        _speedManager.ResumeNormal();
    }

    private void CycleFastForward()
    {
        if (IsBlocked)
        {
            return;
        }

        SoundManager.Play(SoundId.UiButtonClick);
        _speedManager.CycleFastForward();
    }

    // 막혔으면 클릭음도 내지 않는다 - 소리가 나면 눌린 줄 알고 다시 누르게 된다.
    private bool IsBlocked => BlockQuery != null && !BlockQuery.CanUseHudControl();

    private void OnEnable()
    {
        if (_speedManager != null)
        {
            _speedManager.SpeedChanged.AddListener(Render);
            Render();
        }
    }

    private void OnDisable()
    {
        if (_speedManager != null)
        {
            _speedManager.SpeedChanged.RemoveListener(Render);
        }
    }

    private void Render()
    {
        bool isPaused = _speedManager.IsPaused;
        bool isFastForward = _speedManager.IsFastForward;
        bool isNormal = !isPaused && !isFastForward;

        if (_selectPause != null)
        {
            _selectPause.SetActive(isPaused);
        }

        if (_selectPlay != null)
        {
            _selectPlay.SetActive(isNormal);
        }

        if (_selectSpeedUp != null)
        {
            _selectSpeedUp.SetActive(isFastForward);
        }

        if (_speedUpLabel != null)
        {
            float displayScale = isFastForward ? _speedManager.CurrentScale : _speedManager.FirstFastScale;
            _speedUpLabel.text = string.Format(
                StringTable.GetString(SPEED_MULTIPLIER_FORMAT_LOC_KEY),
                displayScale);
        }

        bool interactable = !_speedManager.IsSpeedLocked;
        if (_buttonPause != null)
        {
            _buttonPause.interactable = interactable;
        }

        if (_buttonPlay != null)
        {
            _buttonPlay.interactable = interactable;
        }

        if (_buttonSpeedUp != null)
        {
            _buttonSpeedUp.interactable = interactable;
        }
    }
}
