using UnityEngine;

/// <summary>
/// 효과음 하나의 재생 설정. SoundCatalog 애셋의 인스펙터에서 편집한다.
/// 수치를 여기에 모아 두는 이유: 재생 코드에 볼륨·피치 같은 리터럴 상수가 박히지 않게 하려는 것
/// (CLAUDE.md 4번). 클립을 아직 못 받은 항목은 Clip을 비워 두면 재생 시 조용히 무시된다.
/// </summary>
[System.Serializable]
public class SoundEntry
{
    [SerializeField] private SoundId _id;

    [Tooltip("비워 두면 이 사운드는 재생되지 않는다(애셋 수급 전 정상 상태).")]
    [SerializeField] private AudioClip _clip;

    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Tooltip("재생마다 이 범위에서 피치를 무작위로 고른다. x=y면 항상 같은 피치. " +
        "타워 발사음처럼 짧은 간격으로 반복되는 소리의 기계적인 느낌을 없앤다.")]
    [SerializeField] private Vector2 _pitchRange = Vector2.one;

    [Tooltip("같은 사운드를 다시 재생하기까지의 최소 간격(초). 0이면 제한 없음. " +
        "타워 여러 개가 같은 프레임에 발사할 때 같은 소리가 겹쳐 커지는 것을 막는다.")]
    [Min(0f)]
    [SerializeField] private float _minInterval;

    public SoundId Id => _id;
    public AudioClip Clip => _clip;
    public float Volume => _volume;
    public float MinInterval => _minInterval;

    /// <summary>이번 재생에 쓸 피치. 범위가 역순으로 설정돼 있어도 동작하도록 양끝을 정렬해 뽑는다.</summary>
    public float NextPitch =>
        Random.Range(Mathf.Min(_pitchRange.x, _pitchRange.y), Mathf.Max(_pitchRange.x, _pitchRange.y));
}
