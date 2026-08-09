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

    public UnityEvent OnInventoryChanged = new();

    public GridMap Map;

    /// <summary>
    /// 세이브 복원 전용. 어미용 속성과 새끼용·알 목록, 주기 보상 수령 이력을 저장값으로 갈아 끼운다.
    /// 보상 이력까지 복원해야 불러오기 후에도 주기당 1회 지급 제한이 유지된다.
    /// CurrentDragon 인스턴스 자체는 교체하지 않는다 - GameManager.Awake에서 Construct로 걸어 둔
    /// CycleManager 구독(하루 1회 속성 변경 제한)이 끊기기 때문이다.
    /// CurrentCycle은 CycleManager.SeedRestoredDay가 담당하므로 여기서 건드리지 않는다.
    /// </summary>
    public void RestoreInventory(
        DragonType dragonType,
        List<BabyDragon> babyDragons,
        List<DragonEgg> dragonEggs,
        List<BossDragonEggReward> bossDragonEggRewards)
    {
        if (CurrentDragon != null)
        {
            CurrentDragon.CurrentType = dragonType;
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
}


public enum DragonType
{
    Ice,
    Fire,
    Time,
    Stone,
    Life
}

[Serializable]
public class Dragon
{
    public DragonType CurrentType;
    public bool IsChangedThisDay;

    public UnityAction OnDragonTypeChanged;

    private CycleManager _cyclemanager;

    public void Construct(CycleManager cycleManager)
    {
        _cyclemanager = cycleManager;
        _cyclemanager.OnDayStart.AddListener(ResetChangedThisDay);
    }

    public bool TryChangeType(DragonType type)
    {
        if (IsChangedThisDay)
            return false;

        CurrentType = type;
        IsChangedThisDay = true;
        return true;
    }

    public void ResetChangedThisDay(int _)
    {
        IsChangedThisDay = false;
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
