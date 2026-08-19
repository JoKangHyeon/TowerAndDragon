using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도감 해금 이력의 단일 소유자. PlayerPrefs 키 하나에 JSON을 통째로 넣는다
/// (SettingsService의 settings_key_bindings와 같은 방식).
///
/// static인 이유: 이 사실은 씬 오브젝트가 아니라 <b>프로세스 전역에 하나뿐인 플레이어 단위 사실</b>이고,
/// 씬을 넘나들며(타이틀 → 본게임) 살아 있어야 한다. 프로젝트가 금지하는 것은 "씬 오브젝트를
/// Instance 게터로 전역 접근하는 것"이지 이런 정적 사실이 아니다 - StringTable이 같은 형태로
/// 로드된 딕셔너리를 들고 OnLanguageChanged까지 낸다.
///
/// 저장 매체를 나중에 파일(persistentDataPath + SaveFileStore.TryWriteAtomic)로 옮기더라도
/// 고칠 곳은 이 파일의 Load/Save 두 메서드뿐이다. PlayerPrefs에는 원자적 쓰기·손상 격리가 없고
/// Windows에서는 레지스트리에 묻혀 QA가 파일을 첨부할 수 없다는 점을 알고 쓴다.
/// </summary>
public static class HelpProfile
{
    private const string PROFILE_PREF_KEY = "help_profile";

    // 읽을 수 없는 값을 지우지 않고 옮겨 두는 자리. 도감 이력은 잃어도 게임은 돌아야 하지만
    // 복구 여지는 남긴다.
    private const string CORRUPT_BACKUP_PREF_KEY = "help_profile_corrupt";

    private static HashSet<string> _unlockedIds;

    /// <summary>해금 목록이 바뀌었다. 도감 창이 열린 채로 해금되면 목록을 다시 그린다.</summary>
    public static event Action Changed;

    public static IReadOnlyCollection<string> UnlockedIds
    {
        get
        {
            EnsureLoaded();
            return _unlockedIds;
        }
    }

    public static bool IsUnlocked(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return false;
        }

        EnsureLoaded();
        return _unlockedIds.Contains(entryId);
    }

    /// <summary>
    /// 처음 해금하는 것이면 true를 돌려주고 즉시 기록한다. 이미 있으면 false
    /// (RunData.TryCompleteObjective와 같은 계약 - 부르는 쪽은 true일 때만 팝업을 띄운다).
    /// </summary>
    public static bool TryUnlock(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return false;
        }

        EnsureLoaded();

        if (!_unlockedIds.Add(entryId))
        {
            return false;
        }

        Save();
        Changed?.Invoke();
        return true;
    }

    private static void EnsureLoaded()
    {
        if (_unlockedIds != null)
        {
            return;
        }

        _unlockedIds = new HashSet<string>();

        string json = PlayerPrefs.GetString(PROFILE_PREF_KEY, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        if (!SaveJson.TryDeserialize(json, out HelpProfileDto dto, out string error))
        {
            Debug.LogWarning($"[HelpProfile] 해금 이력을 읽지 못해 빈 프로필로 시작합니다: {error}");
            PlayerPrefs.SetString(CORRUPT_BACKUP_PREF_KEY, json);
            PlayerPrefs.DeleteKey(PROFILE_PREF_KEY);
            PlayerPrefs.Save();
            return;
        }

        if (dto.UnlockedEntryIds == null)
        {
            return;
        }

        foreach (string entryId in dto.UnlockedEntryIds)
        {
            if (!string.IsNullOrWhiteSpace(entryId))
            {
                _unlockedIds.Add(entryId);
            }
        }
    }

    // 해금은 세션당 몇 번뿐이고 저장물은 수 KB다. "게임을 끄니 도감이 사라졌다"가 이 기능의
    // 최악 버그이므로 미루지 않고 그 자리에서 쓴다.
    // (SettingsService가 종료 시점에 몰아 쓰는 것은 슬라이더가 초당 수십 번 바뀌기 때문이다.)
    private static void Save()
    {
        HelpProfileDto dto = new HelpProfileDto
        {
            SchemaVersion = HelpProfileDto.CURRENT_VERSION,
            UnlockedEntryIds = new List<string>(_unlockedIds),
        };

        if (!SaveJson.TrySerialize(dto, out string json, out string error))
        {
            Debug.LogWarning($"[HelpProfile] 해금 이력을 저장하지 못했습니다: {error}");
            return;
        }

        PlayerPrefs.SetString(PROFILE_PREF_KEY, json);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    /// <summary>검증용. 해금 이력을 전부 지운다(Tools 메뉴에서 부른다).</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(PROFILE_PREF_KEY);
        PlayerPrefs.DeleteKey(CORRUPT_BACKUP_PREF_KEY);
        PlayerPrefs.Save();

        _unlockedIds = null;
        Changed?.Invoke();
    }

    // Reload Domain이 꺼진 프로젝트 설정에서는 플레이 종료 후에도 static이 그대로 남는다.
    // 캐시는 PlayerPrefs와 항상 같으므로 문제되지 않지만, 이전 세션의 구독자가 남으면
    // 파괴된 창을 향해 이벤트가 날아간다 - 플레이 시작마다 명시적으로 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _unlockedIds = null;
        Changed = null;
    }
#endif
}
