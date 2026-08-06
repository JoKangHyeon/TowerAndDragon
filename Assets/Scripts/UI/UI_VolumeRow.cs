using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 창 사운드 섹션의 볼륨 한 줄(마스터/배경음악/효과음).
/// *_Row 오브젝트에 붙이고, 슬라이더·퍼센트 라벨·화살표 버튼을 인스펙터로 지정한다.
/// UI_ConfigWindow가 자식에서 모아 Construct로 의존성을 넣어주므로, 창 쪽에 배열을 두지 않는다.
/// </summary>
public class UI_VolumeRow : MonoBehaviour
{
    // stringtable — config_volume_percent : "{0}%"
    private const string VOLUME_PERCENT_LOC_KEY = "config_volume_percent";

    // 0~1 볼륨을 퍼센트 표기로 바꿀 때 쓰는 배율.
    private const float PERCENT_SCALE = 100f;

    private const int STEP_DECREASE = -1;
    private const int STEP_INCREASE = 1;

    [Tooltip("이 줄이 조절할 믹서 채널.")]
    [SerializeField] private AudioChannel _channel;
    [SerializeField] private Slider _slider;
    [Tooltip("현재 볼륨을 퍼센트로 보여주는 텍스트.")]
    [SerializeField] private TMP_Text _percentText;
    [Tooltip("좌측 화살표. 한 단계 낮춘다.")]
    [SerializeField] private Button _decreaseButton;
    [Tooltip("우측 화살표. 한 단계 올린다.")]
    [SerializeField] private Button _increaseButton;

    private SettingsService _settings;
    private float _step;

    /// <summary>UI_ConfigWindow가 한 번 호출해 의존성을 넣고 입력을 연결한다.</summary>
    public void Construct(SettingsService settings, float step)
    {
        _settings = settings;
        _step = step;

        if (_slider != null)
        {
            _slider.onValueChanged.AddListener(SetVolume);
        }

        if (_decreaseButton != null)
        {
            _decreaseButton.onClick.AddListener(() => StepVolume(STEP_DECREASE));
        }

        if (_increaseButton != null)
        {
            _increaseButton.onClick.AddListener(() => StepVolume(STEP_INCREASE));
        }
    }

    public void Render()
    {
        if (_settings == null)
        {
            return;
        }

        float volume = _settings.GetVolume(_channel);

        // SetValueWithoutNotify로 넣어야 onValueChanged가 되돌아와 무한 루프가 되지 않는다.
        if (_slider != null)
        {
            _slider.SetValueWithoutNotify(volume);
        }

        if (_percentText != null)
        {
            _percentText.text = string.Format(
                StringTable.GetString(VOLUME_PERCENT_LOC_KEY),
                Mathf.RoundToInt(volume * PERCENT_SCALE));
        }
    }

    private void SetVolume(float normalizedVolume)
    {
        if (_settings == null)
        {
            return;
        }

        _settings.SetVolume(_channel, normalizedVolume);
        Render();
    }

    private void StepVolume(int direction)
    {
        SoundManager.Play(SoundId.UiButtonClick);

        if (_settings == null)
        {
            return;
        }

        SetVolume(_settings.GetVolume(_channel) + (_step * direction));
    }
}
