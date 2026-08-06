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
