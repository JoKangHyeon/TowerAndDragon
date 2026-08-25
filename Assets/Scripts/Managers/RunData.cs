using System;
using System.Collections.Generic;
using UnityEngine.Events;

[Serializable]
public class RunData
{
    public int CurrentCycle;

    public Dragon CurrentDragon;
    public List<BabyDragon> BabyDragons = new();
    public List<DragonEgg> DragonEggs = new();
    public List<BossDragonEggReward> BossDragonEggRewards = new();

    // "완료한" 단계가 아니라 "진입한" 단계를 기록한다 - 안내는 단계에 들어선 순간 떠야 하므로,
    // 종료 조건으로 기록하면 화살표가 한 박자 늦는다.
    public List<BabyDragonGuideStep> EnteredGuideSteps = new();

    // 1일차 튜토리얼 진행도. 단계가 enum이 아니라 에셋이므로 순서 번호가 아니라 StepId로 남긴다 -
    // 번호로 남기면 중간에 단계를 끼워 넣는 순간 기존 진행도가 다른 단계를 가리킨다.
    public List<string> EnteredTutorialStepIds = new();

    // 끝까지 봤거나 건너뛴 경우. 둘을 구분하지 않는 이유는 어느 쪽이든 다시 뜨면 안 되기 때문이다.
    public bool IsTutorialDismissed;

    // 자유 목표는 위 EnteredTutorialStepIds와 따로 둔다 - 저쪽은 "진입", 이쪽은 "완료"라 의미가 다르다.
    // 하나로 합치면 목록에 띄우기만 한 목표가 완료로 기록된다.
    public List<string> CompletedTutorialObjectiveIds = new();

    // 가이드 퀘스트는 위 자유 목표와 목록을 공유하지 않는다 - 저쪽은 튜토리얼 씬에서만 도는 목표이고
    // 이쪽은 본게임의 빌드업 체크리스트라, 하나로 합치면 튜토리얼을 건너뛴 플레이어의 세이브에
    // 튜토리얼 목표 완료 기록이 섞여 들어간다.
    public List<string> CompletedGuideQuestIds = new();

    // 조언자 카드에 답했는가. 수준 자체는 여기 두지 않는다(플레이어 설정이라 SettingsService 소관) -
    // 남길 것은 "이 런에서 이미 물어봤다"뿐이다.
    public bool IsGuideIntroAnswered;

    public UnityEvent OnInventoryChanged = new();

    public GridMap Map;

    /// <summary>
    /// 세이브 복원 전용. 어미용 속성과 새끼용·알 목록, 주기 보상 수령 이력을 저장값으로 갈아 끼운다.
    /// 보상 이력까지 복원해야 불러오기 후에도 주기당 1회 지급 제한이 유지된다.
    /// 속성 변경 카운터도 같은 이유로 함께 복원한다 - 비우면 굳은 맹세(sworn_element)의
    /// 주기당 제한이 저장 -> 불러오기만으로 한 번 되살아난다.
    /// CurrentDragon 인스턴스 자체는 교체하지 않는다 - 다른 곳에서 잡고 있는 참조가 끊기기 때문이다.
    /// CurrentCycle은 CycleManager.SeedRestoredDay가 담당하므로 여기서 건드리지 않는다.
    /// </summary>
    public void RestoreInventory(
        DragonType dragonType,
        List<BabyDragon> babyDragons,
        List<DragonEgg> dragonEggs,
        List<BossDragonEggReward> bossDragonEggRewards,
        int typeChangeCycleNumber,
        int typeChangeCountInCycle)
    {
        if (CurrentDragon != null)
        {
            CurrentDragon.CurrentType = dragonType;
            CurrentDragon.RestoreTypeChangeCounter(typeChangeCycleNumber, typeChangeCountInCycle);
        }

        BabyDragons.Clear();
        BabyDragons.AddRange(babyDragons);

        DragonEggs.Clear();
        DragonEggs.AddRange(dragonEggs);

        BossDragonEggRewards ??= new List<BossDragonEggReward>();
        BossDragonEggRewards.Clear();
        BossDragonEggRewards.AddRange(bossDragonEggRewards);

        OnInventoryChanged?.Invoke();
    }
    
    public bool HasClaimedBossDragonEggReward(int cycleNumber)
    {
        if (BossDragonEggRewards == null)
        {
            return false;
        }

        foreach (BossDragonEggReward reward in BossDragonEggRewards)
        {
            if (reward != null && reward.CycleNumber == cycleNumber)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryRecordBossDragonEggReward(int cycleNumber, DragonType dragonType)
    {
        if (HasClaimedBossDragonEggReward(cycleNumber))
        {
            return false;
        }

        BossDragonEggRewards ??= new List<BossDragonEggReward>();
        BossDragonEggRewards.Add(new BossDragonEggReward
        {
            CycleNumber = cycleNumber,
            DragonType = dragonType,
        });
        return true;
    }

    public bool HasEnteredGuideStep(BabyDragonGuideStep step)
    {
        return EnteredGuideSteps != null && EnteredGuideSteps.Contains(step);
    }

    public bool TryEnterGuideStep(BabyDragonGuideStep step)
    {
        if (HasEnteredGuideStep(step))
        {
            return false;
        }

        EnteredGuideSteps ??= new List<BabyDragonGuideStep>();
        EnteredGuideSteps.Add(step);
        return true;
    }

    public bool HasEnteredTutorialStep(string stepId)
    {
        return EnteredTutorialStepIds != null && EnteredTutorialStepIds.Contains(stepId);
    }

    public bool TryEnterTutorialStep(string stepId)
    {
        if (string.IsNullOrWhiteSpace(stepId) || HasEnteredTutorialStep(stepId))
        {
            return false;
        }

        EnteredTutorialStepIds ??= new List<string>();
        EnteredTutorialStepIds.Add(stepId);
        return true;
    }

    public bool HasCompletedObjective(string objectiveId)
    {
        return CompletedTutorialObjectiveIds != null && CompletedTutorialObjectiveIds.Contains(objectiveId);
    }

    /// <summary>이미 완료한 목표면 false - 완료 알림이 두 번 뜨지 않게 호출부가 이 반환값으로 거른다.</summary>
    public bool TryCompleteObjective(string objectiveId)
    {
        if (string.IsNullOrWhiteSpace(objectiveId) || HasCompletedObjective(objectiveId))
        {
            return false;
        }

        CompletedTutorialObjectiveIds ??= new List<string>();
        CompletedTutorialObjectiveIds.Add(objectiveId);
        return true;
    }

    public bool HasCompletedGuideQuest(string questId)
    {
        return CompletedGuideQuestIds != null && CompletedGuideQuestIds.Contains(questId);
    }

    /// <summary>
    /// 이미 완료한 퀘스트면 false - 보상 중복 지급과 완료 알림 재표시를 막는 유일한 관문이다.
    /// (RunData.TryCompleteObjective와 같은 계약)
    /// </summary>
    public bool TryCompleteGuideQuest(string questId)
    {
        if (string.IsNullOrWhiteSpace(questId) || HasCompletedGuideQuest(questId))
        {
            return false;
        }

        CompletedGuideQuestIds ??= new List<string>();
        CompletedGuideQuestIds.Add(questId);
        return true;
    }

    /// <summary>세이브 복원 전용. 가이드 퀘스트 진행도를 저장값으로 갈아 끼운다.</summary>
    public void RestoreGuideQuests(List<string> completedQuestIds, bool isIntroAnswered)
    {
        CompletedGuideQuestIds ??= new List<string>();
        CompletedGuideQuestIds.Clear();
        CompletedGuideQuestIds.AddRange(completedQuestIds);
        IsGuideIntroAnswered = isIntroAnswered;
    }
}


public enum DragonType
{
    Ice,
    Fire,
    Time,
    Stone,
    Life
}

/// <summary>어미용 속성 변경이 거절된 이유. 호출부가 사유 문구를 고르는 데 쓴다 -
/// bool 하나로는 "같은 속성이라 아무 일도 없음"과 "제한에 걸려 막힘"을 구분할 수 없다.
/// DragonTreeManager.TryUnlock(out ProgressionFailureReason)과 같은 형태다.</summary>
public enum DragonTypeChangeBlock
{
    None,
    SameType,
    CycleLimitReached,
}

[Serializable]
public class Dragon
{
    public DragonType CurrentType;

    // 굳은 맹세(sworn_element)의 주기당 변경 제한을 세는 상태.
    // 주기 전환 이벤트를 구독하지 않고 "마지막으로 센 주기 번호"를 들고 비교한다 -
    // WaveCycleProgression.CycleStarted는 불러오기 때의 따라잡기 호출에서도 다시 발화하므로,
    // 그걸로 리셋하면 저장→불러오기만으로 그 주기의 변경권이 되살아난다.
    //
    // 두 필드 모두 세이브에 실린다(RunStateDto.TypeChangeCycleNumber/TypeChangeCountInCycle) -
    // 싣지 않으면 저장 -> 불러오기만으로 그 주기의 변경권이 한 번 되살아난다.
    public int TypeChangeCycleNumber;
    public int TypeChangeCountInCycle;

    public UnityAction OnDragonTypeChanged;

    /// <summary>어미용 속성을 바꾼다. 이미 그 속성이면 아무것도 하지 않는다.
    /// 반환값은 "실제로 바뀌었는가" - 호출부는 이 값으로 속성 변경 알림 발화 여부를 가른다.
    ///
    /// <paramref name="limitPerCycle"/>은 <b>0이면 제한 없음</b>이다 - 뮤테이터 카운터 채널의
    /// 항등원이 0이라 "뮤테이터 미선택 = 무제한"이 자연스럽게 성립한다.
    /// <paramref name="cycleNumber"/>가 지난번과 다르면 카운터를 먼저 리셋한다(주기 리셋 지점).</summary>
    public bool TryChangeType(
        DragonType type,
        int cycleNumber,
        int limitPerCycle,
        out DragonTypeChangeBlock block)
    {
        if (CurrentType == type)
        {
            block = DragonTypeChangeBlock.SameType;
            return false;
        }

        SyncCycle(cycleNumber);

        if (IsCycleLimitReached(limitPerCycle))
        {
            block = DragonTypeChangeBlock.CycleLimitReached;
            return false;
        }

        CurrentType = type;
        TypeChangeCountInCycle += 1;
        block = DragonTypeChangeBlock.None;
        return true;
    }

    /// <summary>이번 주기의 변경권을 이미 다 썼는가. 버튼 비활성화처럼 <b>시도 전에</b> 물어보는 곳이 쓴다.
    /// 조회만 하며 카운터를 건드리지 않는다.</summary>
    public bool IsCycleLimitReached(int cycleNumber, int limitPerCycle)
    {
        if (limitPerCycle <= DragonTypeChangeRules.UNLIMITED)
        {
            return false;
        }

        // 다른 주기의 기록이면 아직 한 번도 안 쓴 것과 같다.
        return TypeChangeCycleNumber == cycleNumber && TypeChangeCountInCycle >= limitPerCycle;
    }

    /// <summary>세이브 복원 전용. 저장된 카운터를 그대로 되돌린다.
    /// 기록이 없는 세이브(구버전·표준 모드)는 0/0으로 들어와 "아직 안 씀"이 된다.</summary>
    public void RestoreTypeChangeCounter(int cycleNumber, int countInCycle)
    {
        // Mathf를 쓰지 않는다 - 이 파일은 UnityEngine을 참조하지 않는 순수 데이터 클래스다.
        TypeChangeCycleNumber = cycleNumber > 0 ? cycleNumber : 0;
        TypeChangeCountInCycle = countInCycle > 0 ? countInCycle : 0;
    }

    private void SyncCycle(int cycleNumber)
    {
        if (TypeChangeCycleNumber == cycleNumber)
        {
            return;
        }

        TypeChangeCycleNumber = cycleNumber;
        TypeChangeCountInCycle = 0;
    }

    private bool IsCycleLimitReached(int limitPerCycle)
    {
        return limitPerCycle > DragonTypeChangeRules.UNLIMITED &&
               TypeChangeCountInCycle >= limitPerCycle;
    }
}

[Serializable]
public class BabyDragon
{
    public string DragonName;
    public DragonType DragonType;

    public bool IsInTower;

    // 버프모드/공격모드 선택 - 철거 후 재설치해도 유지되도록 레코드에 저장한다.
    // IsModeInitialized가 false인 동안은 Mode(기본값 Attack)가 "아직 정해지지 않음"을 뜻한다 -
    // BabyDragonTower.BindRecord가 데이터 기반 기본값(공격 불가면 Buff)으로 한 번 채운다.
    public BabyDragonMode Mode;
    public bool IsModeInitialized;
}

[Serializable]
public class DragonEgg
{
    public DragonType DragonType;
    public int FedDayCount; 
}

[Serializable]
public sealed class BossDragonEggReward
{
    public int CycleNumber;
    public DragonType DragonType;
}
