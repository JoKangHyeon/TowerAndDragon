using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <see cref="SaveGameDto"/>를 라이브 게임 상태에 적용한다.
/// DTO와 게임 타입 사이의 변환은 전부 여기서만 한다 - 매니저는 DTO의 존재를 모른다.
///
/// 호출 전제:
///  - dto.TryNormalize()가 이미 통과했다(컬렉션이 null이 아니고 값이 검증됐다).
///  - 모든 오브젝트의 Start가 끝난 뒤다(특히 Castle.Start의 홈 청크 점령과 체력 초기화).
///  - 호출자가 마지막에 CycleManager.ResumeDay()를 부른다. 이 클래스는 부르지 않는다.
///    StartDay가 아니라 ResumeDay인 이유: 스냅샷은 이미 정산이 끝난 상태이므로
///    생산·유지비·알 성장을 재생하면 안 된다(SaveService 계약 2).
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
        // 1. 일차·페이즈 시드. 이후 모든 핸들러가 일관된 일차를 보게 한다.
        context.CycleManager.SeedRestoredDay(dto.Run.CurrentCycle);

        // 2. 자원. 복원된 재고가 곧 오늘의 재고다 - 뒤에 재생할 정산이 없다.
        RestoreResources(dto.Resources, context.ResourceManager);

        // 3. 총 인구. 8번(원정 인구 배치)보다 반드시 앞서야 한다 -
        //    TryAssign은 가용 인구가 모자라면 조용히 실패한다.
        if (context.PopulationManager != null)
        {
            context.PopulationManager.RestoreMaxPopulation(dto.Population.MaxPopulation);
        }

        // 4. 연구.
        if (context.ResearchManager != null)
        {
            context.ResearchManager.RestoreProgress(
                dto.Research.ResearchPoints,
                dto.Research.UnlockedNodeIds);
        }

        // 5. 어미용 속성과 새끼용·알 인벤토리. 6번보다 앞서야 한다 -
        //    RestoreUnlockedNodes가 재발화하는 NodeUnlocked를 CastleVisionCoordinator가 받아
        //    시야 보너스를 계산하므로, 그때 속성이 이미 복원돼 있어야 한다.
        //    RestoreInventory는 데이터만 세팅하므로 스킬트리에 의존하지 않는다.
        RestoreRun(dto.Run, context.GameManager.CurrentRun);

        // 6. 용 스킬트리.
        if (context.DragonTreeManager != null)
        {
            context.DragonTreeManager.RestoreUnlockedNodes(dto.DragonTree.UnlockedNodeIds);
            context.DragonTreeManager.NotifyActiveAttributeChanged();
        }

        // 7. 랜드마크 수령 이력. 8번(영토 복원)보다 반드시 앞서야 한다 -
        //    RestoreTerritory가 GridMap.OnChunkStateChanged를 발화시키고,
        //    LandmarkManager가 그걸 받아 점령된 랜드마크를 수령하려 들기 때문이다.
        //    이력을 먼저 넣어두지 않으면 불러올 때마다 알과 자원이 다시 지급된다.
        context.LandmarkManager?.RestoreClaims(dto.Landmarks.ClaimedLandmarkIds);

        // 8·9. 청크 상태와 진행 중 원정.
        if (context.ConquestManager != null)
        {
            context.ConquestManager.RestoreTerritory(
                Vector2IntDto.ToVector2IntList(dto.Map.VisibleChunks),
                Vector2IntDto.ToVector2IntList(dto.Map.ConqueredChunks),
                Vector2IntDto.ToVector2IntList(dto.Map.EnhancedChunks));

            context.ConquestManager.RestoreExpeditions(ToExpeditions(dto.Conquest));
        }

        // 10. 랜드마크 배치 인구. 8번(영토 복원) 이후여야 한다 - 미점령 랜드마크에는
        //     인구를 넣을 수 없으므로(LandmarkPopulation.CanChangePopulation),
        //     점령 상태가 반영된 뒤에 배치해야 한다.
        //     3번(총 인구)보다도 뒤다 - 가용 인구가 모자라면 조용히 실패한다.
        if (context.LandmarkManager != null)
        {
            context.LandmarkManager.ClearAllOperations();

            foreach (LandmarkOperationDto operation in dto.Landmarks.Operations)
            {
                context.LandmarkManager.RestoreOperation(
                    operation.LandmarkId,
                    operation.AssignedPopulation);
            }

            // 8번의 영토 복원이 이미 같은 갱신을 유발하지만, GridMap은 점령지 집합이 실제로
            // 바뀔 때만 이벤트를 쏜다. 복원 전후 점령지가 같은 경우(진행 중인 게임에 같은 슬롯을
            // 다시 불러오는 등) 갱신이 한 번도 안 돌아 이미 수령한 랜드마크가 맵에 남으므로,
            // 여기서 한 번 명시적으로 훑는다.
            context.LandmarkManager.RefreshConquestState();
        }

        // TODO(범위 밖): 여기에 건물 배치 복원이 들어간다. MapStateDto.Buildings 주석 참고.

        // 11. 성 체력. 다른 복원값에 의존하지 않으므로 마지막에 둔다 -
        //    Castle.Start의 Initialize(만피)를 여기서 덮어쓰는 편이 읽기 쉽다.
        //    0 이하는 "기록 없음"(성이 연결되지 않은 씬에서 저장한 슬롯)이므로 만피를 유지한다.
        if (dto.Castle.CurrentHealth > 0f)
        {
            context.Castle?.RestoreHealth(dto.Castle.CurrentHealth);
        }

        // 12. ResumeDay는 호출자(SaveService)가 부른다 - 복원 실패 시 폴백 경로와 구분하기 위해
        //     이 클래스는 상태 적용까지만 책임진다.
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

        var bossDragonEggRewards = new List<BossDragonEggReward>();

        foreach (BossDragonEggRewardDto entry in dto.BossDragonEggRewards)
        {
            bossDragonEggRewards.Add(new BossDragonEggReward
            {
                CycleNumber = entry.CycleNumber,
                DragonType = (DragonType)entry.DragonType,
            });
        }

        run.RestoreInventory(
            (DragonType)dto.DragonType,
            babyDragons,
            dragonEggs,
            bossDragonEggRewards);
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
