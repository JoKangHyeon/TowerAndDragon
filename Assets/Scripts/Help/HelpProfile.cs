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

    // 해금과 따로 둔다 - "만났다"와 "읽었다"는 소비자가 다르다(전자는 목록, 후자는 붉은 점).
    private static HashSet<string> _viewedIds;

    /// <summary>해금 목록이 바뀌었다. 도감 창이 열린 채로 해금되면 목록을 다시 그린다.</summary>
    public static event Action Changed;

    /// <summary>
    /// 열람 이력이 바뀌었다(붉은 점 하나가 사라졌다). Changed와 나누는 이유는 소비자가 다르기
    /// 때문이다 - 이 신호로는 목록 구성·정렬·자동 선택이 절대 바뀌지 않으므로, 받는 쪽은
    /// 목록을 재구성할 필요가 없다.
    ///
    /// <b>렌더 경로 안에서는 이 이벤트를 유발하지 않는다.</b> UI_HelpWindow는 이것을 구독해
    /// 다시 그리므로, 렌더 도중에 TryMarkViewed를 부르면 Render가 자기 안으로 재진입한다
    /// (UI_HelpWindow.MarkSelectedViewed 주석에 무엇이 깨지는지 적어 두었다).
    /// </summary>
    public static event Action ViewedChanged;

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

    /// <summary>도감 창에서 본문을 펼쳐 본 항목인가. 붉은 점을 지우는 조건은 해금이 아니라 이쪽이다.</summary>
    public static bool IsViewed(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return false;
        }

        EnsureLoaded();
        return _viewedIds.Contains(entryId);
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

    /// <summary>
    /// 처음 열람한 것이면 true를 돌려주고 즉시 기록한다(TryUnlock과 같은 계약).
    /// 최초 조우 팝업은 이것을 부르지 않는다 - 붉은 점의 의미를 "도감에서 직접 펼쳐 봤는가"
    /// 하나로 유지한다(항목 대부분이 AutoPopup이라, 팝업을 세면 점이 사실상 뜨지 않는다).
    /// </summary>
    public static bool TryMarkViewed(string entryId)
    {
        if (string.IsNullOrWhiteSpace(entryId))
        {
            return false;
        }

        EnsureLoaded();

        if (!_viewedIds.Add(entryId))
        {
            return false;
        }

        Save();
        ViewedChanged?.Invoke();
        return true;
    }

    private static void EnsureLoaded()
    {
        if (_unlockedIds != null)
        {
            return;
        }

        // 두 집합은 같은 JSON 하나에서 나오므로 아래 이른 return들보다 앞에서 함께 만든다 -
        // 읽기에 실패해도 _viewedIds가 null로 남으면 IsViewed가 터진다.
        _unlockedIds = new HashSet<string>();
        _viewedIds = new HashSet<string>();

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

        AddAll(dto.UnlockedEntryIds, _unlockedIds);

        // v1에는 이 목록이 없어 역직렬화 후 빈 채로 남는다(구버전 해금분은 전부 미확인으로 잡힌다).
        // 팝업으로 봤을 뿐 도감에서 펼쳐 본 적은 없으므로 그게 사실에 맞다.
        AddAll(dto.ViewedEntryIds, _viewedIds);
    }

    private static void AddAll(List<string> source, HashSet<string> target)
    {
        if (source == null)
        {
            return;
        }

        foreach (string entryId in source)
        {
            if (!string.IsNullOrWhiteSpace(entryId))
            {
                target.Add(entryId);
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
            ViewedEntryIds = new List<string>(_viewedIds),
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
    /// <summary>검증용. 해금·열람 이력을 전부 지운다(Tools 메뉴에서 부른다).</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(PROFILE_PREF_KEY);
        PlayerPrefs.DeleteKey(CORRUPT_BACKUP_PREF_KEY);
        PlayerPrefs.Save();

        _unlockedIds = null;
        _viewedIds = null;
        Changed?.Invoke();
        ViewedChanged?.Invoke();
    }

    /// <summary>
    /// 검증용. 해금은 남기고 열람 이력만 지운다(Tools 메뉴에서 부른다).
    /// 항목 대부분이 AutoPopup이라, 이것 없이 붉은 점을 다시 보려면 한 판을 다시 해서
    /// 최초 조우 트리거를 전부 다시 밟아야 한다.
    /// </summary>
    public static void ResetViewedOnly()
    {
        EnsureLoaded();

        _viewedIds.Clear();
        Save();
        ViewedChanged?.Invoke();
    }

    // Reload Domain이 꺼진 프로젝트 설정에서는 플레이 종료 후에도 static이 그대로 남는다.
    // 캐시는 PlayerPrefs와 항상 같으므로 문제되지 않지만, 이전 세션의 구독자가 남으면
    // 파괴된 창을 향해 이벤트가 날아간다 - 플레이 시작마다 명시적으로 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _unlockedIds = null;
        _viewedIds = null;
        Changed = null;
        ViewedChanged = null;
    }
#endif
}
