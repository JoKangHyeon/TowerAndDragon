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
    /// </summary>
    public static void Apply(SaveGameDto dto, SaveCaptureContext context)
    {
        // 0. 새 게임 +(뮤테이터). 2번(자원)·10번(랜드마크 인구)·11번(건물)보다 반드시 앞서야 한다 -
        //    생산 배율·유지비·타워 최대 체력이 전부 이 스냅샷을 전제로 계산되므로, 뒤에 적용하면
        //    복원된 수치가 표준 모드 기준으로 굳은 뒤 뮤테이터만 켜진 어긋난 상태가 된다.
        //    미지 id·범위 밖 단계 거부는 SaveService가 여기 오기 전에 이미 끝냈다(롤백 불가 구간).
        context.RunModifierService?.ApplyRestoredMutators(ToMutatorSelections(dto.Run.Mutators));

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

        //    가이드 퀘스트 진행도도 RunData에만 쓰므로 같은 자리에서 함께 복원한다.
        //    다른 시스템을 건드리지 않아 순서 의존성이 없다 - 표시(UI_GuideQuestWindow)는
        //    ResumeDay 뒤에 오는 QuestsChanged로 따라온다.
        context.GameManager.CurrentRun.RestoreGuideQuests(
            dto.GuideQuests.CompletedQuestIds,
            dto.GuideQuests.IsIntroAnswered);

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

        // 11. 건물 배치. 8번(영토) 이후여야 한다 - OnBuildingAdded 구독자들이 청크 상태를 읽는다.
        //     9·10번(원정·랜드마크 인구)보다 뒤인 것이 핵심이다. 셋이 같은 인구 총량을 다투는데,
        //     실패의 성질이 다르다 - 원정·랜드마크가 인구를 못 받으면 비용 없이 진행되는 조용한 오염이
        //     되지만, 건물은 "인구 0인 건물"이라는 눈에 보이고 로그가 남고 다시 배치할 수 있는 상태로 끝난다.
        //     총량이 모자란 세이브에서는 건물이 마지막에 줍게 한다.
        RestoreBuildings(dto.Map.Buildings, dto.Run, context);

        // 12. 성 체력. 다른 복원값에 의존하지 않으므로 마지막에 둔다 -
        //    Castle.Start의 Initialize(만피)를 여기서 덮어쓰는 편이 읽기 쉽다.
        //    0 이하는 "기록 없음"(성이 연결되지 않은 씬에서 저장한 슬롯)이므로 만피를 유지한다.
        if (dto.Castle.CurrentHealth > 0f)
        {
            context.Castle?.RestoreHealth(dto.Castle.CurrentHealth);
        }

        // 13. ResumeDay는 호출자(SaveService)가 부른다 - 복원 실패 시 폴백 경로와 구분하기 위해
        //     이 클래스는 상태 적용까지만 책임진다.
    }

    /// <summary>
    /// 건물을 다시 짓고 인구를 다시 배치한다. 두 패스로 나눈 이유는 배치 순서 의존을 없애기 위함이다 -
    /// TowerPopulation.TryAssign은 건물이 운영 중단(IsSuspended)이면 조용히 실패하는데,
    /// 화염지대 건물의 운영 중단은 얼음 새끼용 버프로 풀린다. 그 새끼용이 목록 뒤쪽이면
    /// 한 패스로 처리할 때 아직 배치 전이라 멀쩡한 건물이 인구 0으로 복원된다.
    ///
    /// 한 건물이 실패해도 나머지는 계속 복원한다 - Apply는 롤백이 불가능한 구간이라 중단이 더 나쁘다.
    /// </summary>
    private static void RestoreBuildings(
        List<BuildingPlacementDto> placements,
        RunStateDto run,
        SaveCaptureContext context)
    {
        if (placements.Count == 0)
        {
            return;
        }

        if (context.GridMap == null || context.BuildingCatalog == null)
        {
            Debug.LogWarning(
                $"[SaveRestore] GridMap 또는 BuildingCatalog가 연결되지 않아 건물 {placements.Count}개를 복원하지 못했습니다.");

            return;
        }

        var restored = new List<(BuildingPlacementDto Placement, Building Instance)>();

        // 패스 1 - 배치.
        foreach (BuildingPlacementDto placement in placements)
        {
            if (!context.BuildingCatalog.TryGetPrefab(placement.PrefabId, out Building prefab))
            {
                Debug.LogError(
                    $"[SaveRestore] PrefabId '{placement.PrefabId}'가 BuildingCatalog에 없어 복원하지 못했습니다.");

                continue;
            }

            BabyDragon babyDragon = ResolveBabyDragonRecord(placement, context);

            // 결속할 레코드가 없는 새끼용 타워는 아예 짓지 않는다 - 데이터 없는 유령이 남는 것보다 낫다.
            if (prefab is BabyDragonTower && babyDragon == null)
            {
                Debug.LogError(
                    $"[SaveRestore] 새끼용 타워 '{placement.PrefabId}'에 결속할 보유 레코드가 없어 복원하지 못했습니다.");

                continue;
            }

            // BabyDragonPlacementCoordinator.HandleBuildingAdded가 이 레코드를 보고
            // Setup·스프라이트·BindRecord·IsInTower를 전부 처리한다(배치 경로와 같은 코드).
            context.BabyDragonPlacementCoordinator?.PrepareRestoreBinding(babyDragon);

            Building instance = context.GridMap.RestoreBuilding(
                prefab,
                placement.Anchor.ToVector3Int(),
                placement.RotationSteps);

            // 배치가 실패했을 때 예약이 다음 건물로 새지 않도록 즉시 해제한다.
            context.BabyDragonPlacementCoordinator?.PrepareRestoreBinding(null);

            if (instance == null)
            {
                Debug.LogError(
                    $"[SaveRestore] '{placement.PrefabId}'를 {placement.Anchor.ToVector3Int()}에 놓지 못했습니다.");

                continue;
            }

            instance.SetConstructedCycle(placement.ConstructedCycle);
            restored.Add((placement, instance));
        }

        // 패스 2 - 인구.
        foreach ((BuildingPlacementDto placement, Building instance) in restored)
        {
            if (placement.AssignedPopulation <= 0)
            {
                continue;
            }

            var target = instance.GetComponent<IPopulationAllocationTarget>();

            if (target == null || !target.TryAssign(placement.AssignedPopulation))
            {
                Debug.LogWarning(
                    $"[SaveRestore] '{placement.PrefabId}'에 인구 {placement.AssignedPopulation}명을 배치하지 못했습니다.");
            }
        }

        // 패스 3 - 타워 체력·부활 진행도. 패스 2 뒤인 이유는 부활 게이지의 진행 속도가 배치 인구에
        // 좌우돼(Tower.ReviveAfterDelayAsync의 StaffingRatio) 인구가 확정된 뒤에 얹는 편이 읽기 쉽기
        // 때문이다. Tower.RestoreHealth는 Setup 전에 불려도 값을 받아 두므로 프레임 순서에 의존하지 않는다.
        foreach ((BuildingPlacementDto placement, Building instance) in restored)
        {
            if (instance is Tower tower)
            {
                tower.RestoreHealth(
                    placement.CurrentHealth,
                    placement.IsDisabled,
                    placement.ReviveProgress);
            }
        }

        Debug.Log($"[SaveRestore] 건물 복원 - 요청 {placements.Count}개 중 {restored.Count}개 배치");
        WarnOnUnplacedBabyDragons(run, restored);
    }

    private static BabyDragon ResolveBabyDragonRecord(
        BuildingPlacementDto placement,
        SaveCaptureContext context)
    {
        if (placement.BabyDragonIndex == BuildingPlacementDto.NO_BABY_DRAGON_INDEX)
        {
            return null;
        }

        // 인덱스 유효성은 SaveGameDto.TryNormalize가 DTO 기준으로 이미 검증했고
        // RestoreInventory가 그 목록을 1:1로 복사하므로 여기서 어긋날 일은 없다.
        // 그래도 범위를 확인하는 이유: 어긋나면 예외가 나면서 복원 전체가 씬 리로드로 날아간다.
        List<BabyDragon> babyDragons = context.GameManager.CurrentRun.BabyDragons;

        if (placement.BabyDragonIndex >= babyDragons.Count)
        {
            return null;
        }

        return babyDragons[placement.BabyDragonIndex];
    }

    /// <summary>
    /// 저장 당시 설치돼 있었는데 복원되지 않은 새끼용을 알린다. 게임 상태는 이미 일관적이지만
    /// (RestoreRun이 IsInTower를 false로 시작하고 BindRecord만 true로 되돌린다)
    /// 배치가 조용히 사라진 것을 로그 없이 넘기지 않기 위한 관측이다.
    /// </summary>
    private static void WarnOnUnplacedBabyDragons(
        RunStateDto run,
        List<(BuildingPlacementDto Placement, Building Instance)> restored)
    {
        var placedIndices = new HashSet<int>();

        foreach ((BuildingPlacementDto placement, Building _) in restored)
        {
            placedIndices.Add(placement.BabyDragonIndex);
        }

        for (int i = 0; i < run.BabyDragons.Count; i++)
        {
            if (run.BabyDragons[i].IsInTower && !placedIndices.Contains(i))
            {
                Debug.LogWarning(
                    $"[SaveRestore] 저장 당시 설치돼 있던 새끼용 {i}번이 복원되지 않아 인벤토리로 돌아갑니다.");
            }
        }
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

                // 설치 여부는 저장값이 아니라 11단계의 건물 배치 복원이 정한다 - false로 시작해
                // BabyDragonPlacementCoordinator.BindRecord가 실제로 세워진 개체만 true로 되돌린다.
                // 저장값을 그대로 넣으면 배치 복원이 실패했을 때 "탑 안에 있다고 주장하는데 그 탑이 없는"
                // 유령이 남지만, 이 방향이면 실제 배치 결과에서 파생되므로 자기치유된다.
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
            bossDragonEggRewards,
            dto.TypeChangeCycleNumber,
            dto.TypeChangeCountInCycle);
    }

    /// <summary>세이브의 뮤테이터 목록을 RunModifierService·RunMutatorCatalogSO가 쓰는 (id, 단계) 튜플로 옮긴다.
    /// SaveService의 복원 전 거부 판정과 여기의 실제 적용이 <b>같은 변환</b>을 쓰게 하려고 공개해 둔다 -
    /// 둘이 갈라지면 "검사는 통과했는데 적용은 다른 목록"이 된다.</summary>
    public static List<(string Id, int Tier)> ToMutatorSelections(List<RunMutatorSelectionDto> selections)
    {
        var result = new List<(string Id, int Tier)>();

        if (selections == null)
        {
            return result;
        }

        foreach (RunMutatorSelectionDto selection in selections)
        {
            result.Add((selection.Id, selection.Tier));
        }

        return result;
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
