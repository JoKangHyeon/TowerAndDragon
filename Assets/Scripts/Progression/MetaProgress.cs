using System;
using UnityEngine;

/// <summary>런을 넘어 남는 진행도(메타 프로그레션)의 단일 소유자.
///
/// 같은 폴더의 ProgressionTree·ProgressionGateSO 계열이 <b>런 내부</b>의 해금(연구·용 스킬트리)을
/// 다루는 것과 달리, 이쪽은 <b>런을 넘는 계정 단위</b> 진행도다. 그래서 세이브 슬롯이 아니라
/// PlayerPrefs에 저장한다 - 슬롯을 지워도 "한 번 클리어했다"는 사실은 남아야 한다.
///
/// <see cref="SettingsService"/>와 같은 형태로 <b>PlayerPrefs 키를 private const로 독점 소유</b>한다.
/// 이 클래스 밖에서 이 키 문자열을 쓰지 않는다.
/// </summary>
public static class MetaProgress
{
    // PlayerPrefs 키. 이 스크립트 밖에서는 쓰지 않는다.
    private const string FIRST_CLEAR_PREF_KEY = "progress_first_clear";
    private const string BEST_DIFFICULTY_PREF_KEY = "progress_best_difficulty";

    private const int PREF_FLAG_OFF = 0;
    private const int PREF_FLAG_ON = 1;

    private const int NO_SCORE = 0;

    /// <summary>진행도가 실제로 바뀌었을 때만 발화한다. 화면이 그 자리에서 갱신돼야 하는 곳이 구독한다
    /// (<see cref="UI_TitleWindow"/>의 "새 게임 +" 버튼).
    ///
    /// 정적 이벤트를 쓰는 이유: 해금을 일으키는 쪽(설정창의 시연용 아이콘)은 3개 씬이 공유하는
    /// 프리팹 안에 있고, 그 표시를 반영해야 하는 쪽은 StartScene에만 있다. 인스펙터 참조로 이으면
    /// 씬마다 배선이 갈라지고 인게임 인스턴스에는 넣을 대상조차 없다.
    /// <see cref="StringTable.OnLanguageChanged"/>와 같은 관용구이며, 구독은 OnEnable /
    /// 해제는 OnDisable로 짝을 맞춘다.</summary>
    public static event Action Changed;

    /// <summary>한 번이라도 클리어했는가. "새 게임 +"의 공개 조건이다.</summary>
    public static bool IsFirstClearDone =>
        PlayerPrefs.GetInt(FIRST_CLEAR_PREF_KEY, PREF_FLAG_OFF) == PREF_FLAG_ON;

    /// <summary>클리어한 런의 최고 난이도 점수. 표시 전용이며 게임 내 능력을 주지 않는다.</summary>
    public static int BestDifficultyScore =>
        PlayerPrefs.GetInt(BEST_DIFFICULTY_PREF_KEY, NO_SCORE);

    /// <summary>클리어를 기록한다. 표준 모드 클리어는 <paramref name="difficultyScore"/>가 0이다.
    /// 중복 호출 방어는 부르는 쪽(<see cref="GameManager"/>의 TrySetGameResult)이 이미 한다.</summary>
    public static void RecordClear(int difficultyScore)
    {
        bool hasChanged = false;

        if (!IsFirstClearDone)
        {
            PlayerPrefs.SetInt(FIRST_CLEAR_PREF_KEY, PREF_FLAG_ON);
            hasChanged = true;
        }

        if (difficultyScore > BestDifficultyScore)
        {
            PlayerPrefs.SetInt(BEST_DIFFICULTY_PREF_KEY, difficultyScore);
            hasChanged = true;
        }

        if (hasChanged)
        {
            Flush();
        }
    }

    /// <summary>심사·시연용 강제 해금. 세이브를 날린 유저를 위한 숏컷도 겸한다(설계서 §7.4).
    ///
    /// <b>실제로 무언가 바뀌었을 때만 true다.</b> 부르는 쪽이 "이미 해금돼 있었다"와
    /// "지금 해금됐다"를 구분해 피드백을 낼 수 있어야 한다 - 조용히 성공하면 시연 중에
    /// 눌렸는지 알 수 없고, 이미 해금된 상태에서 또 축하 연출이 나오면 무엇이 일어났는지 헷갈린다.</summary>
    public static bool UnlockForDemo()
    {
        if (IsFirstClearDone)
        {
            return false;
        }

        PlayerPrefs.SetInt(FIRST_CLEAR_PREF_KEY, PREF_FLAG_ON);
        Flush();
        return true;
    }

    /// <summary>에디터·QA용 초기화. 해금 전 화면을 다시 확인할 때 쓴다.</summary>
    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(FIRST_CLEAR_PREF_KEY);
        PlayerPrefs.DeleteKey(BEST_DIFFICULTY_PREF_KEY);
        Flush();
    }

    // Reload Domain이 꺼져 있으면 이전 플레이 세션의 구독이 남아 파괴된 오브젝트를 가리킨다
    // (WiringGuard.ResetStatics와 같은 이유·같은 시점).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearSubscribers()
    {
        Changed = null;
    }

    // 기록마다 디스크에 남긴다 - 강제 종료 시 유실을 막는다. 통지도 여기서 한 번만 한다.
    private static void Flush()
    {
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
