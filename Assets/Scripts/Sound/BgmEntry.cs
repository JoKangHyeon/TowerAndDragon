using UnityEngine;

/// <summary>
/// 배경음 하나의 재생 설정. 루프는 SoundManager가 강제하므로 여기서는 다루지 않는다.
/// </summary>
[System.Serializable]
public class BgmEntry
{
    [SerializeField] private BgmId _id;

    [Tooltip("비워 두면 이 BGM 요청은 무시된다(애셋 수급 전 정상 상태).")]
    [SerializeField] private AudioClip _clip;

    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    public BgmId Id => _id;
    public AudioClip Clip => _clip;
    public float Volume => _volume;
}
