using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="SaveGameDto"/>를 라이브 게임 상태에 적용한다.
/// DTO와 게임 타입 사이의 변환은 전부 여기서만 한다 - 매니저는 DTO의 존재를 모른다.
///
/// 호출 전제:
///  - dto.TryNormalize()가 이미 통과했다(컬렉션이 null이 아니고 값이 검증됐다).
///  - 모든 오브젝트의 Start가 끝난 뒤다(특히 Castle.Start의 홈 청크 점령).
///  - 호출자가 마지막에 CycleManager.StartDay()를 부른다. 이 클래스는 부르지 않는다.
/// </summary>
public static class SaveRestore
{
    /// <summary>
    /// 단계 사이에 순서 의존성이 있다. 바꾸기 전에 각 단계의 주석을 확인할 것.
    /// 건물 배치 복원이 추가되면 청크 복원과 원정 복원 사이에 들어가야 한다
    /// (건물 건설은 점령된 청크를 요구하고, 건물 인구가 원정 인구와 총량을 다툰다).
    /// </summary>
    public static void Apply(SaveGameDto dto, SaveCaptureContext context)
    {
        // 1. 일차 시드. 이후 모든 핸들러가 일관된 일차를 보게 한다.
        //    StartDay가 +1 하므로 여기서는 N-1이 들어간다(CycleManager.RestoreDay 주석 참고).
        context.CycleManager.RestoreDay(dto.Run.CurrentCycle);

        // 2. 자원. 마지막 StartDay가 재생할 유지비 소비가 복원된 재고에서 차감돼야 한다.
        RestoreResources(dto.Resources, context.ResourceManager);

        // 3. 총 인구. 8번(원정 인구 배치)보다 반드시 앞서야 한다 -
        //    TryAssign은 가용 인구가 모자라면 조용히 실패한다.
        if (context.PopulationManager != null)
        {
            context.PopulationManager.RestoreMaxPopulation(dto.Population.MaxPopulation);
        }

        // 4. 연구. 마지막 StartDay가 재생할 정산이 연구 배율·정원을 반영해야 한다.
        if (context.ResearchManager != null)
        {
            context.ResearchManager.RestoreProgress(
                dto.Research.ResearchPoints,
                dto.Research.UnlockedNodeIds);
        }

        // 5. 용 스킬트리.
        if (context.DragonTreeManager != null)
        {
            context.DragonTreeManager.RestoreUnlockedNodes(dto.DragonTree.UnlockedNodeIds);
        }

        // 6. 어미용 속성과 새끼용·알 인벤토리. 5번 뒤에 둬서 스킬트리 게이트가 복원된
        //    속성을 보게 하고, 마지막 StartDay의 알 성장이 복원된 FedDayCount에서 출발하게 한다.
        RestoreRun(dto.Run, context.GameManager.CurrentRun);

        if (context.DragonTreeManager != null)
        {
            context.DragonTreeManager.NotifyActiveAttributeChanged();
        }

        // 7·8. 청크 상태와 진행 중 원정.
        if (context.ConquestManager != null)
        {
            context.ConquestManager.RestoreTerritory(
                Vector2IntDto.ToVector2IntList(dto.Map.VisibleChunks),
                Vector2IntDto.ToVector2IntList(dto.Map.ConqueredChunks),
                Vector2IntDto.ToVector2IntList(dto.Map.EnhancedChunks));

            context.ConquestManager.RestoreExpeditions(ToExpeditions(dto.Conquest));
        }

        // TODO(범위 밖): 여기에 건물 배치 복원이 들어간다. MapStateDto.Buildings 주석 참고.

        // 9. StartDay는 호출자(SaveService)가 부른다 - 복원 실패 시 폴백 경로와 구분하기 위해
        //    이 클래스는 상태 적용까지만 책임진다.
    }

    private static void RestoreResources(ResourceStateDto dto, ResourceManager resourceManager)
    {
        if (resourceManager == null)
        {
            return;
        }

        var amounts = new List<ResourceAmount>();

        foreach (ResourceAmountDto entry in dto.Amounts)
        {
            amounts.Add(new ResourceAmount
            {
                Type = (ResourceType)entry.Type,
                Amount = entry.Amount,
            });
        }

        resourceManager.RestoreAmounts(amounts);
    }

    private static void RestoreRun(RunStateDto dto, RunData run)
    {
        var babyDragons = new List<BabyDragon>();

        foreach (BabyDragonDto entry in dto.BabyDragons)
        {
            babyDragons.Add(new BabyDragon
            {
                DragonName = entry.DragonName,
                DragonType = (DragonType)entry.DragonType,

                // TODO(범위 밖): 타워(건물) 배치가 복원되지 않는 동안은 무조건 false로 강제한다.
                //   true로 두면 "탑 안에 있다고 주장하는데 그 탑이 없는" 유령 상태가 된다.
                //   건물 복원이 들어오면 BabyDragonTower.BindRecord가 다시 세팅하므로 그때 제거한다.
                IsInTower = false,

                Mode = (BabyDragonMode)entry.Mode,
                IsModeInitialized = entry.IsModeInitialized,
            });
        }

        var dragonEggs = new List<DragonEgg>();

        foreach (DragonEggDto entry in dto.DragonEggs)
        {
            dragonEggs.Add(new DragonEgg
            {
                DragonType = (DragonType)entry.DragonType,
                FedDayCount = entry.FedDayCount,
            });
        }

        run.RestoreInventory((DragonType)dto.DragonType, babyDragons, dragonEggs);
    }

    private static List<ConquestExpedition> ToExpeditions(ConquestStateDto dto)
    {
        var expeditions = new List<ConquestExpedition>();

        foreach (ConquestExpeditionDto entry in dto.ActiveExpeditions)
        {
            expeditions.Add(new ConquestExpedition(
                entry.TargetChunkCoord.ToVector2Int(),
                entry.Cost.ToResourceCost(),
                entry.DaysRequired,
                entry.DaysProgressed));
        }

        return expeditions;
    }
}
