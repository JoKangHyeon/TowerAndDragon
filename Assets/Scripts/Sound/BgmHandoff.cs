using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 씬을 넘어 BGM을 이어 재생하기 위해 넘겨받는 재생 상태.
/// <see cref="SoundManager.TryHandOffBgm"/>가 만들고 <see cref="SceneLoadOverlay"/>가 받아 자기 소스로 이어 튼다.
///
/// 위치를 초가 아니라 샘플로 들고 가는 이유는 소수점 오차 없이 끊긴 자리에 정확히 이어붙기 위해서다.
/// 믹서 그룹까지 함께 넘기는 이유는, 그룹 없이 재생하면 믹서를 통째로 건너뛰어 BGM 볼륨 설정이
/// 이 소리에만 먹지 않기 때문이다(볼륨은 SettingsService가 믹서 파라미터로 소유한다).
/// </summary>
public readonly struct BgmHandoff
{
    public readonly AudioClip Clip;
    public readonly int TimeSamples;
    public readonly float Volume;
    public readonly AudioMixerGroup OutputGroup;

    public BgmHandoff(AudioClip clip, int timeSamples, float volume, AudioMixerGroup outputGroup)
    {
        Clip = clip;
        TimeSamples = timeSamples;
        Volume = volume;
        OutputGroup = outputGroup;
    }
}
