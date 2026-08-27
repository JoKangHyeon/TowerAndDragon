using UnityEngine;

/// <summary>
/// 게임에 존재하는 전체 사운드 목록. SoundManager가 id로 클립과 재생 설정을 찾을 때 사용한다.
/// (ResourceCatalog와 같은 구조 - 배열 하나 + TryGet 선형 탐색.)
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Sound Catalog", fileName = "SoundCatalog")]
public class SoundCatalog : ScriptableObject
{
    [SerializeField] private SoundEntry[] _sounds;
    [SerializeField] private BgmEntry[] _bgms;

    [Header("타워 효과음 화면 기준 재생")]

    [Tooltip("타워·새끼용 전투음 전체에 곱하는 배수. 항목별 Volume의 상대 밸런스는 유지한 채 전투음만 한꺼번에 올리고 내린다. " +
        "UI·건설·몬스터·BGM에는 영향이 없다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _seTowerVolumeScale = 1f;

    [Tooltip("화면 중심에서 이 거리까지는 볼륨을 줄이지 않는다. 화면 가장자리가 1이다.")]
    [Min(0f)]
    [SerializeField] private float _seFullVolumeViewportRatio = 1f;

    [Tooltip("화면 중심에서 이 거리를 넘으면 재생하지 않는다. 화면 가장자리가 1이다.")]
    [Min(0f)]
    [SerializeField] private float _seCutoffViewportRatio = 1.8f;

    [Tooltip("좌우 정위 세기. 0이면 정위 없이 가운데에서 들린다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _sePanAmount = 0.6f;

    [Tooltip("이 orthographicSize 이하로 확대된 상태에서는 줌에 의한 감쇠가 없다.")]
    [Min(0f)]
    [SerializeField] private float _seZoomReferenceSize = 6f;

    [Tooltip("줌아웃으로 아무리 멀어져도 이 볼륨 배수 아래로는 내려가지 않는다.")]
    [Range(0f, 1f)]
    [SerializeField] private float _seMinZoomVolumeScale = 0.4f;

    public float SeTowerVolumeScale => _seTowerVolumeScale;
    public float SeFullVolumeViewportRatio => _seFullVolumeViewportRatio;
    public float SeCutoffViewportRatio => _seCutoffViewportRatio;
    public float SePanAmount => _sePanAmount;
    public float SeZoomReferenceSize => _seZoomReferenceSize;
    public float SeMinZoomVolumeScale => _seMinZoomVolumeScale;

    // 수십 종 수준이라 선형 탐색으로 충분하다(ResourceCatalog.TryGet과 같은 판단).
    public bool TryGet(SoundId id, out SoundEntry entry)
    {
        foreach (SoundEntry sound in _sounds)
        {
            if (sound != null && sound.Id == id)
            {
                entry = sound;
                return true;
            }
        }

        entry = null;
        return false;
    }

    public bool TryGet(BgmId id, out BgmEntry entry)
    {
        foreach (BgmEntry bgm in _bgms)
        {
            if (bgm != null && bgm.Id == id)
            {
                entry = bgm;
                return true;
            }
        }

        entry = null;
        return false;
    }
}
