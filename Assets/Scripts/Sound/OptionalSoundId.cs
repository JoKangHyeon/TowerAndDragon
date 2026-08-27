using UnityEngine;

[System.Serializable]
public struct OptionalSoundId
{
    [Tooltip("끄면 이 사운드는 재생되지 않는다. 값을 채우지 않은 기존 에셋의 기본 상태다.")]
    [SerializeField] private bool _isEnabled;

    [SerializeField] private SoundId _id;

    public SoundId? Value => _isEnabled ? _id : null;
}