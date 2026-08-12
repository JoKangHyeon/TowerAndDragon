using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

/// <summary>
/// 설정값(볼륨·해상도·전체화면·언어)의 단일 소유자.
/// PlayerPrefs에 저장하고, 저장된 값을 실제 시스템(AudioMixer / Screen / StringTable)에 적용한다.
/// UI(UI_ConfigWindow)는 이 서비스만 읽고 쓴다 — 시스템 API를 직접 만지지 않는다.
/// (싱글톤 아님 — SerializeField 주입 관례. 항상 활성인 오브젝트에 둔다.)
/// 저장값 적용은 Awake가 아니라 Start에서 한다(CLAUDE.md 이벤트 초기화 규칙 — 언어 적용이
/// OnLanguageChanged를 발화하므로, 구독자(LocalizedText 등)의 OnEnable이 모두 끝난 뒤여야 한다).
/// </summary>
public class SettingsService : MonoBehaviour
{
    // PlayerPrefs 키. 이 스크립트 밖에서는 쓰지 않는다.
    private const string MASTER_VOLUME_PREF_KEY = "settings_volume_master";
    private const string BGM_VOLUME_PREF_KEY = "settings_volume_bgm";
    private const string SE_VOLUME_PREF_KEY = "settings_volume_se";
    private const string RESOLUTION_WIDTH_PREF_KEY = "settings_resolution_width";
    private const string RESOLUTION_HEIGHT_PREF_KEY = "settings_resolution_height";
    private const string FULL_SCREEN_PREF_KEY = "settings_full_screen";
    private const string LANGUAGE_PREF_KEY = "settings_language";
    private const string KEY_BINDINGS_PREF_KEY = "settings_key_bindings";

    // AudioMixer에 노출된 파라미터 이름. Assets/AudioMixer.mixer의 m_ExposedParameters와 일치해야 한다.
    private const string MASTER_VOLUME_MIXER_PARAM = "MasterVolume";
    private const string BGM_VOLUME_MIXER_PARAM = "BGMVolume";
    private const string SE_VOLUME_MIXER_PARAM = "SEVolume";

    // 0~1 선형 볼륨 → 데시벨. 진폭 기준 표준 변환식 log10(v) * 20을 쓴다.
    private const float DECIBEL_PER_DECADE = 20f;
    // log10(0)은 -무한대이므로, 이 값 이하는 믹서가 무음으로 취급하는 -80dB로 못박는다.
    private const float SILENCE_THRESHOLD = 0.0001f;
    private const float MUTED_DECIBEL = -80f;

    private const float DEFAULT_VOLUME = 0.8f;
    private const int PREF_FLAG_OFF = 0;
    private const int PREF_FLAG_ON = 1;
    private const int NOT_FOUND_INDEX = -1;

    [Tooltip("볼륨 슬라이더가 조절할 믹서. Assets/AudioMixer.mixer를 지정한다.")]
    [SerializeField] private AudioMixer _audioMixer;

    [Tooltip("키 바인딩을 저장·복원할 대상. Assets/InputSystem_Actions.inputactions를 지정한다.")]
    [SerializeField] private InputActionAsset _inputActions;

    /// <summary>키 바인딩이 바뀌었을 때(리바인딩·기본값 복원) 발화한다. UI가 표기를 다시 그린다.</summary>
    public event Action OnKeyBindingsChanged;

    private readonly Dictionary<AudioChannel, float> _volumes = new();
    private readonly List<Vector2Int> _resolutions = new();

    /// <summary>선택 가능한 해상도(가로x세로). 중복 주사율은 제거하고 오름차순으로 정렬한다.</summary>
    public IReadOnlyList<Vector2Int> Resolutions => _resolutions;

    /// <summary>현재 선택된 해상도의 <see cref="Resolutions"/> 내 인덱스.</summary>
    public int ResolutionIndex { get; private set; }

    public bool IsFullScreen { get; private set; }

    /// <summary>선택 가능한 언어 코드 목록(StreamingAssets/Localization의 csv 파일들).</summary>
    public IReadOnlyList<string> Languages => StringTable.AvailableLanguages;

    /// <summary>현재 언어의 <see cref="Languages"/> 내 인덱스. 목록에 없으면 0.</summary>
    public int LanguageIndex
    {
        get
        {
            int index = IndexOfLanguage(StringTable.CurrentLanguage);
            return index == NOT_FOUND_INDEX ? 0 : index;
        }
    }

    private void Awake()
    {
        LoadVolumes();
        BuildResolutionList();
        LoadDisplay();
        LoadKeyBindings();
    }

    private void Start()
    {
        ApplyVolume(AudioChannel.Master);
        ApplyVolume(AudioChannel.BGM);
        ApplyVolume(AudioChannel.SE);
        ApplyDisplay();
        ApplyLanguage();
    }

    // 알트탭·창 닫기 등으로 그냥 종료돼도 값이 남도록 마지막에 한 번 디스크에 밀어 넣는다.
    private void OnApplicationQuit()
    {
        Save();
    }

    public float GetVolume(AudioChannel channel)
    {
        return _volumes.TryGetValue(channel, out float volume) ? volume : DEFAULT_VOLUME;
    }

    public void SetVolume(AudioChannel channel, float normalizedVolume)
    {
        float clamped = Mathf.Clamp01(normalizedVolume);
        _volumes[channel] = clamped;
        PlayerPrefs.SetFloat(VolumePrefKey(channel), clamped);
        ApplyVolume(channel);
    }

    /// <summary>해상도를 인덱스로 지정한다. 범위를 벗어나면 양끝에서 순환한다(Prev/Next 버튼용).</summary>
    public void SetResolutionIndex(int index)
    {
        if (_resolutions.Count == 0)
        {
            return;
        }

        ResolutionIndex = WrapIndex(index, _resolutions.Count);

        Vector2Int resolution = _resolutions[ResolutionIndex];
        PlayerPrefs.SetInt(RESOLUTION_WIDTH_PREF_KEY, resolution.x);
        PlayerPrefs.SetInt(RESOLUTION_HEIGHT_PREF_KEY, resolution.y);
        ApplyDisplay();
    }

    public void SetFullScreen(bool isFullScreen)
    {
        IsFullScreen = isFullScreen;
        PlayerPrefs.SetInt(FULL_SCREEN_PREF_KEY, isFullScreen ? PREF_FLAG_ON : PREF_FLAG_OFF);
        ApplyDisplay();
    }

    /// <summary>
    /// 언어를 인덱스로 지정한다. 실제 전환은 StringTable이 하고, 여기선 저장만 더한다.
    /// 범위를 벗어나면 양끝에서 순환한다(Prev/Next 버튼용).
    /// </summary>
    public void SetLanguageIndex(int index)
    {
        IReadOnlyList<string> languages = Languages;
        if (languages == null || languages.Count == 0)
        {
            return;
        }

        string language = languages[WrapIndex(index, languages.Count)];
        PlayerPrefs.SetString(LANGUAGE_PREF_KEY, language);

        if (language != StringTable.CurrentLanguage)
        {
            StringTable.LoadLanguage(language);
        }
    }

    /// <summary>
    /// 지금 걸려 있는 바인딩 오버라이드를 저장한다. 리바인딩이 확정될 때마다 호출한다
    /// (디스크 플러시는 다른 설정과 마찬가지로 <see cref="Save"/>가 몰아서 한다).
    /// </summary>
    public void SaveKeyBindings()
    {
        // 여기서 조용히 빠져나가면 리바인딩이 한 세션 내내 정상으로 보이다가 재시작에 전부 날아간다.
        if (!WiringGuard.Require(_inputActions, nameof(_inputActions), this))
        {
            return;
        }

        PlayerPrefs.SetString(KEY_BINDINGS_PREF_KEY, _inputActions.SaveBindingOverridesAsJson());
        OnKeyBindingsChanged?.Invoke();
    }

    /// <summary>모든 키를 에셋에 정의된 기본값으로 되돌린다.</summary>
    public void ResetKeyBindings()
    {
        if (!WiringGuard.Require(_inputActions, nameof(_inputActions), this))
        {
            return;
        }

        _inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(KEY_BINDINGS_PREF_KEY);
        OnKeyBindingsChanged?.Invoke();
    }

    public void Save()
    {
        PlayerPrefs.Save();
    }

    // 오버라이드는 에셋 자체에 얹히므로 다른 설정과 달리 Start까지 미룰 필요가 없다.
    // 액션을 켜기 전(각 컴포넌트의 OnEnable 전)에 얹어 두는 편이 안전하다.
    private void LoadKeyBindings()
    {
        if (!WiringGuard.Require(_inputActions, nameof(_inputActions), this))
        {
            return;
        }

        string json = PlayerPrefs.GetString(KEY_BINDINGS_PREF_KEY, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        _inputActions.LoadBindingOverridesFromJson(json);
    }

    private void LoadVolumes()
    {
        _volumes[AudioChannel.Master] = PlayerPrefs.GetFloat(MASTER_VOLUME_PREF_KEY, DEFAULT_VOLUME);
        _volumes[AudioChannel.BGM] = PlayerPrefs.GetFloat(BGM_VOLUME_PREF_KEY, DEFAULT_VOLUME);
        _volumes[AudioChannel.SE] = PlayerPrefs.GetFloat(SE_VOLUME_PREF_KEY, DEFAULT_VOLUME);
    }

    // Screen.resolutions는 같은 해상도를 주사율별로 여러 번 돌려주므로 가로x세로로만 추린다.
    // 에디터에선 목록이 비거나 현재 해상도가 빠질 수 있어, 현재 해상도는 항상 포함시킨다.
    private void BuildResolutionList()
    {
        _resolutions.Clear();

        foreach (Resolution resolution in Screen.resolutions)
        {
            AddResolution(new Vector2Int(resolution.width, resolution.height));
        }

        AddResolution(new Vector2Int(Screen.width, Screen.height));

        _resolutions.Sort(CompareResolution);
    }

    private void AddResolution(Vector2Int resolution)
    {
        if (!_resolutions.Contains(resolution))
        {
            _resolutions.Add(resolution);
        }
    }

    private static int CompareResolution(Vector2Int left, Vector2Int right)
    {
        int byWidth = left.x.CompareTo(right.x);
        return byWidth != 0 ? byWidth : left.y.CompareTo(right.y);
    }

    private void LoadDisplay()
    {
        IsFullScreen = PlayerPrefs.GetInt(
            FULL_SCREEN_PREF_KEY,
            Screen.fullScreen ? PREF_FLAG_ON : PREF_FLAG_OFF) == PREF_FLAG_ON;

        var saved = new Vector2Int(
            PlayerPrefs.GetInt(RESOLUTION_WIDTH_PREF_KEY, Screen.width),
            PlayerPrefs.GetInt(RESOLUTION_HEIGHT_PREF_KEY, Screen.height));

        // 저장된 해상도가 목록에 없으면(모니터 교체 등) 가장 큰 해상도를 고른다.
        int index = _resolutions.IndexOf(saved);
        ResolutionIndex = index == NOT_FOUND_INDEX ? _resolutions.Count - 1 : index;
    }

    private void ApplyVolume(AudioChannel channel)
    {
        if (!WiringGuard.Require(_audioMixer, nameof(_audioMixer), this))
        {
            return;
        }

        _audioMixer.SetFloat(MixerParam(channel), ToDecibel(GetVolume(channel)));
    }

    private void ApplyDisplay()
    {
        if (_resolutions.Count == 0)
        {
            return;
        }

        Vector2Int resolution = _resolutions[ResolutionIndex];
        Screen.SetResolution(resolution.x, resolution.y, IsFullScreen);
    }

    private void ApplyLanguage()
    {
        string saved = PlayerPrefs.GetString(LANGUAGE_PREF_KEY, StringTable.c_DefaultLanguage);

        // 저장된 언어의 csv가 사라졌을 수 있으므로 목록에 있는지 확인하고, 없으면 기본 언어로 되돌린다.
        string language =
            IndexOfLanguage(saved) == NOT_FOUND_INDEX ? StringTable.c_DefaultLanguage : saved;

        if (language != StringTable.CurrentLanguage)
        {
            StringTable.LoadLanguage(language);
        }
    }

    private static int IndexOfLanguage(string language)
    {
        IReadOnlyList<string> languages = StringTable.AvailableLanguages;
        if (languages == null || string.IsNullOrEmpty(language))
        {
            return NOT_FOUND_INDEX;
        }

        for (int i = 0; i < languages.Count; i++)
        {
            if (languages[i] == language)
            {
                return i;
            }
        }

        return -PREF_FLAG_ON;
    }

    private static int WrapIndex(int index, int count)
    {
        return ((index % count) + count) % count;
    }

    private static float ToDecibel(float normalizedVolume)
    {
        return normalizedVolume <= SILENCE_THRESHOLD
            ? MUTED_DECIBEL
            : Mathf.Log10(normalizedVolume) * DECIBEL_PER_DECADE;
    }

    private static string VolumePrefKey(AudioChannel channel)
    {
        return channel switch
        {
            AudioChannel.BGM => BGM_VOLUME_PREF_KEY,
            AudioChannel.SE => SE_VOLUME_PREF_KEY,
            _ => MASTER_VOLUME_PREF_KEY,
        };
    }

    private static string MixerParam(AudioChannel channel)
    {
        return channel switch
        {
            AudioChannel.BGM => BGM_VOLUME_MIXER_PARAM,
            AudioChannel.SE => SE_VOLUME_MIXER_PARAM,
            _ => MASTER_VOLUME_MIXER_PARAM,
        };
    }
}
