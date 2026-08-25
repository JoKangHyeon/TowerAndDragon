using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 라이브 게임 상태를 <see cref="SaveGameDto"/>로 찍는다.
///
/// 캡처에 필요한 값은 전부 기존 public 읽기 API로 열려 있어, 이 클래스를 위해 매니저를 수정하지
/// 않는다(복원 쪽만 매니저에 좁은 진입점을 추가했다).
/// 반드시 메인 스레드에서 동기로 호출한다 - GridMap 등 Unity API를 만진다.
/// </summary>
public static class SaveCapture
{
    public static SaveGameDto Capture(SaveCaptureContext context, int slotIndex, bool isAutoSave)
    {
        RunData run = context.GameManager.CurrentRun;

        var dto = new SaveGameDto
        {
            Run = CaptureRun(run, context.CycleManager, context.RunModifierService),
            Resources = CaptureResources(context.ResourceManager),
            Population = CapturePopulation(context.PopulationManager),
            Research = CaptureResearch(context.ResearchManager),
            DragonTree = CaptureDragonTree(context.DragonTreeManager),
            Conquest = CaptureConquest(context.ConquestManager),
            Map = CaptureMap(context.ConquestManager, context.GridMap, run),
            Castle = CaptureCastle(context.Castle),
            Landmarks = CaptureLandmarks(context.LandmarkManager),
            GuideQuests = CaptureGuideQuests(run),
        };

        dto.Meta = CaptureMeta(context, dto.Run, dto.Resources, slotIndex, isAutoSave);
        return dto;
    }

    private static SaveMetaDto CaptureMeta(
        SaveCaptureContext context,
        RunStateDto run,
        ResourceStateDto resources,
        int slotIndex,
        bool isAutoSave)
    {
        return new SaveMetaDto
        {
            SchemaVersion = SaveSchema.CURRENT_VERSION,
            GameVersion = Application.version,

            // 로컬 시각으로 저장하면 서머타임 전환 구간(가을에 같은 시각이 두 번 오는 1시간)에서
            // 어느 쪽인지 알 수 없게 된다. 표시 시점에 ToLocalTime으로 되돌린다.
            SavedAtUtc = DateTimeOffset.UtcNow,

            SlotIndex = slotIndex,
            IsAutoSave = isAutoSave,
            DayNumber = run.CurrentCycle,

            // 자동저장은 OnDaySettled(= OnDayReady 이후)에 붙으므로, 이 시점에는
            // WaveCycleProgression이 이미 오늘의 주기 스냅샷을 확정해 둔 상태다.
            // 정산 전 경계에서 찍던 시절에는 어제 값이 새어 나왔다.
            CycleNumber = context.WaveCycleProgression != null
                ? context.WaveCycleProgression.CurrentCycleNumber
                : 0,
            DragonType = run.DragonType,

            // 서비스가 없는 씬(튜토리얼·테스트)에서는 0점 = 표준 모드로 기록된다.
            DifficultyScore = context.RunModifierService != null
                ? context.RunModifierService.DifficultyScore
                : 0,

            // 본문에서 이미 만든 목록을 그대로 참조한다 - 다시 순회하지 않고, 본문과 메타가
            // 서로 다른 값을 담을 여지도 없앤다.
            Resources = resources.Amounts,
        };
    }

    private static RunStateDto CaptureRun(
        RunData run,
        CycleManager cycleManager,
        RunModifierService runModifierService)
    {
        var dto = new RunStateDto
        {
            CurrentCycle = run.CurrentCycle,
            CyclePhase = (int)cycleManager.CurrentCycle,
            DragonType = run.CurrentDragon != null ? (int)run.CurrentDragon.CurrentType : 0,
            BabyDragons = new List<BabyDragonDto>(),
            DragonEggs = new List<DragonEggDto>(),
            BossDragonEggRewards = new List<BossDragonEggRewardDto>(),
            Mutators = CaptureMutators(runModifierService),

            // 굳은 맹세(sworn_element)의 주기당 변경 카운터. 뮤테이터가 꺼진 런에서는 늘 0/0이라
            // 이 필드가 없던 시절의 세이브와 바이트가 같다.
            TypeChangeCycleNumber =
                run.CurrentDragon != null ? run.CurrentDragon.TypeChangeCycleNumber : 0,
            TypeChangeCountInCycle =
                run.CurrentDragon != null ? run.CurrentDragon.TypeChangeCountInCycle : 0,
        };

        foreach (BabyDragon babyDragon in run.BabyDragons)
        {
            dto.BabyDragons.Add(new BabyDragonDto
            {
                DragonName = babyDragon.DragonName,
                DragonType = (int)babyDragon.DragonType,
                IsInTower = babyDragon.IsInTower,
                Mode = (int)babyDragon.Mode,
                IsModeInitialized = babyDragon.IsModeInitialized,
            });
        }

        foreach (DragonEgg egg in run.DragonEggs)
        {
            dto.DragonEggs.Add(new DragonEggDto
            {
                DragonType = (int)egg.DragonType,
                FedDayCount = egg.FedDayCount,
            });
        }

        foreach (BossDragonEggReward reward in run.BossDragonEggRewards)
        {
            dto.BossDragonEggRewards.Add(new BossDragonEggRewardDto
            {
                CycleNumber = reward.CycleNumber,
                DragonType = (int)reward.DragonType,
            });
        }

        return dto;
    }

    // 서비스가 없는 씬(튜토리얼·테스트)에서는 빈 목록으로 캡처된다 - 복원 쪽이 그것을 표준 모드로 읽는다.
    private static List<RunMutatorSelectionDto> CaptureMutators(RunModifierService runModifierService)
    {
        var selections = new List<RunMutatorSelectionDto>();

        if (runModifierService == null)
        {
            return selections;
        }

        foreach (RunMutatorSelection selection in runModifierService.ActiveSelections)
        {
            if (selection.Mutator == null)
            {
                continue;
            }

            selections.Add(new RunMutatorSelectionDto
            {
                Id = selection.Mutator.Id,
                Tier = selection.Tier,
            });
        }

        // ToSortedList와 같은 이유로 정렬한다 - 같은 상태면 같은 바이트가 나오게.
        selections.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return selections;
    }

    private static ResourceStateDto CaptureResources(ResourceManager resourceManager)
    {
        var dto = new ResourceStateDto { Amounts = new List<ResourceAmountDto>() };

        if (resourceManager == null || resourceManager.Catalog == null)
        {
            return dto;
        }

        foreach (ResourceData resource in resourceManager.Catalog.All)
        {
            if (resource == null)
            {
                continue;
            }

            dto.Amounts.Add(new ResourceAmountDto
            {
                Type = (int)resource.Type,
                Amount = resourceManager.GetAmount(resource.Type),
            });
        }

        return dto;
    }

    // 성이 연결되지 않은 씬(테스트 씬 등)에서는 0을 남긴다 - 복원이 "기록 없음"으로 보고 건너뛴다.
    private static CastleStateDto CaptureCastle(Castle castle)
    {
        return new CastleStateDto
        {
            CurrentHealth = castle != null ? castle.CurrentHealth : 0f,
        };
    }

    private static PopulationStateDto CapturePopulation(PopulationManager populationManager)
    {
        return new PopulationStateDto
        {
            MaxPopulation = populationManager != null ? populationManager.MaxPopulation : 0,
        };
    }

    private static ProgressionStateDto CaptureResearch(ResearchManager researchManager)
    {
        if (researchManager == null)
        {
            return new ProgressionStateDto { UnlockedNodeIds = new List<string>() };
        }

        return new ProgressionStateDto
        {
            UnlockedNodeIds = ToSortedList(researchManager.CompletedNodeIds),
            ResearchPoints = researchManager.ResearchPoints,
        };
    }

    private static ProgressionStateDto CaptureDragonTree(DragonTreeManager dragonTreeManager)
    {
        return new ProgressionStateDto
        {
            UnlockedNodeIds = dragonTreeManager != null
                ? ToSortedList(dragonTreeManager.UnlockedIds)
                : new List<string>(),
        };
    }

    private static ConquestStateDto CaptureConquest(ConquestManager conquestManager)
    {
        var dto = new ConquestStateDto { ActiveExpeditions = new List<ConquestExpeditionDto>() };

        if (conquestManager == null)
        {
            return dto;
        }

        foreach (ConquestExpedition expedition in conquestManager.ActiveExpeditions)
        {
            dto.ActiveExpeditions.Add(new ConquestExpeditionDto
            {
                TargetChunkCoord = Vector2IntDto.From(expedition.TargetChunkCoord),
                Cost = ResourceCostDto.From(expedition.Cost),
                DaysRequired = expedition.DaysRequired,
                DaysProgressed = expedition.DaysProgressed,
            });
        }

        return dto;
    }

    private static MapStateDto CaptureMap(ConquestManager conquestManager, GridMap gridMap, RunData run)
    {
        var dto = new MapStateDto
        {
            VisibleChunks = new List<Vector2IntDto>(),
            ConqueredChunks = new List<Vector2IntDto>(),
            EnhancedChunks = new List<Vector2IntDto>(),
            Buildings = new List<BuildingPlacementDto>(),
        };

        if (gridMap != null)
        {
            // Hidden은 청크의 기본 상태이므로 저장하지 않는다.
            foreach (Chunk chunk in gridMap.GetAllChunks())
            {
                if (chunk.CurrentState == ChunkState.Conquered)
                {
                    dto.ConqueredChunks.Add(Vector2IntDto.From(chunk.ChunkCoord));
                }
                else if (chunk.CurrentState == ChunkState.Visible)
                {
                    dto.VisibleChunks.Add(Vector2IntDto.From(chunk.ChunkCoord));
                }
            }
        }

        if (conquestManager != null)
        {
            dto.EnhancedChunks = Vector2IntDto.From(conquestManager.EnhancedChunkCoords);
        }

        dto.Buildings = CaptureBuildings(gridMap, run);
        return dto;
    }

    private static List<BuildingPlacementDto> CaptureBuildings(GridMap gridMap, RunData run)
    {
        var placements = new List<BuildingPlacementDto>();

        if (gridMap == null)
        {
            return placements;
        }

        foreach (Building building in gridMap.Buildings)
        {
            // PrefabId가 비어 있으면 세이브가 되살릴 방법이 없다 - 성·랜드마크 구조물·임시 방벽이
            // 여기서 걸러진다. 타입별 예외 목록을 두지 않고 프리팹 데이터 하나로 판정한다.
            if (building == null || !building.IsSaveable)
            {
                continue;
            }

            int babyDragonIndex = ResolveBabyDragonIndex(building, run);

            // 어느 레코드에서 온 개체인지 모르면 모드·먹이 상태를 충실히 되살릴 수 없다.
            // (에디터에서 씬에 직접 놓은 새끼용이 이 경우다.)
            if (building is BabyDragonTower &&
                babyDragonIndex == BuildingPlacementDto.NO_BABY_DRAGON_INDEX)
            {
                Debug.LogWarning(
                    "[SaveCapture] 보유 레코드에 결속되지 않은 새끼용 타워가 있어 저장에서 제외합니다.",
                    building);

                continue;
            }

            var placement = new BuildingPlacementDto
            {
                PrefabId = building.PrefabId,
                Anchor = Vector3IntDto.From(building.PlacementAnchor),
                RotationSteps = building.RotationSteps,
                AssignedPopulation =
                    building.GetComponent<IPopulationAllocationTarget>()?.AssignedPopulation ?? 0,
                ConstructedCycle = building.ConstructedCycle,
                BabyDragonIndex = babyDragonIndex,
            };

            // 타워가 아닌 건물은 체력 3필드를 기본값(0/false/0)으로 남긴다 - 복원이 "기록 없음"으로
            // 읽으므로 이 필드가 없던 시절의 세이브와 바이트가 같다.
            //
            // 평소에는 타워도 만피로 캡처된다(저장은 낮에만 되고 아침에 전 타워가 복구되므로).
            // no_morning_restore(긴 밤)를 켠 런에서만 부서진 상태가 낮까지 남는데, 그것을 기록하지
            // 않으면 저장 -> 불러오기만으로 전 타워가 만피로 돌아와 5점짜리 뮤테이터가 세탁된다.
            if (building is Tower tower)
            {
                placement.CurrentHealth = tower.CurrentHealth;
                placement.IsDisabled = tower.IsReviving;
                placement.ReviveProgress = tower.ReviveProgress;
            }

            placements.Add(placement);
        }

        // ToSortedList와 같은 이유로 정렬한다 - 같은 상태면 같은 바이트가 나오게.
        placements.Sort(CompareBuildingPlacements);
        return placements;
    }

    private static int ResolveBabyDragonIndex(Building building, RunData run)
    {
        if (run == null || !(building is BabyDragonTower babyDragonTower) || babyDragonTower.Record == null)
        {
            return BuildingPlacementDto.NO_BABY_DRAGON_INDEX;
        }

        // CaptureRun이 같은 리스트를 같은 순서로 순회하므로 이 인덱스가 RunStateDto.BabyDragons와 일치한다.
        return run.BabyDragons.IndexOf(babyDragonTower.Record);
    }

    private static int CompareBuildingPlacements(BuildingPlacementDto left, BuildingPlacementDto right)
    {
        int byPrefabId = string.CompareOrdinal(left.PrefabId, right.PrefabId);

        if (byPrefabId != 0)
        {
            return byPrefabId;
        }

        int byX = left.Anchor.X.CompareTo(right.Anchor.X);
        return byX != 0 ? byX : left.Anchor.Y.CompareTo(right.Anchor.Y);
    }

    private static LandmarkStateDto CaptureLandmarks(LandmarkManager landmarkManager)
    {
        var dto = new LandmarkStateDto
        {
            ClaimedLandmarkIds = new List<string>(),
            Operations = new List<LandmarkOperationDto>(),
        };

        if (landmarkManager == null)
        {
            return dto;
        }

        dto.ClaimedLandmarkIds = ToSortedList(landmarkManager.ClaimedLandmarkIds);

        // 인구가 배치된 랜드마크만 기록한다 - 0명은 복원할 것이 없다.
        foreach (Landmark landmark in landmarkManager.Landmarks)
        {
            if (landmark.Data == null || landmark.Population == null)
            {
                continue;
            }

            int assignedPopulation = landmark.Population.AssignedPopulation;

            if (assignedPopulation <= 0)
            {
                continue;
            }

            dto.Operations.Add(new LandmarkOperationDto
            {
                LandmarkId = landmark.Data.LandmarkId,
                AssignedPopulation = assignedPopulation,
            });
        }

        return dto;
    }

    private static GuideQuestStateDto CaptureGuideQuests(RunData run)
    {
        return new GuideQuestStateDto
        {
            // 완료 순서는 복원에 쓰이지 않으므로 정렬해 둔다 - 같은 상태가 항상 같은 바이트가 된다.
            CompletedQuestIds = ToSortedList(run.CompletedGuideQuestIds),
            IsIntroAnswered = run.IsGuideIntroAnswered,
        };
    }

    // 같은 상태가 항상 같은 바이트로 저장되도록 정렬한다 - 수동 diff와 회귀 테스트가 쉬워진다.
    private static List<string> ToSortedList(IReadOnlyCollection<string> ids)
    {
        var result = new List<string>(ids);
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}

/// <summary>
/// 캡처·복원이 참조하는 매니저 묶음. SaveService가 [SerializeField]로 모아 넘긴다
/// (싱글톤이 없으므로 전역 조회가 불가능하고, 인자 목록이 길어지는 것도 막는다).
/// </summary>
public readonly struct SaveCaptureContext
{
    public GameManager GameManager { get; }
    public CycleManager CycleManager { get; }
    public ResourceManager ResourceManager { get; }
    public PopulationManager PopulationManager { get; }
    public ResearchManager ResearchManager { get; }
    public DragonTreeManager DragonTreeManager { get; }
    public ConquestManager ConquestManager { get; }
    public GridMap GridMap { get; }
    public WaveCycleProgression WaveCycleProgression { get; }

    /// <summary>메인 성. 다른 매니저와 같이 선택적으로 취급한다(IsValid가 요구하지 않는다).</summary>
    public Castle Castle { get; }

    /// <summary>랜드마크가 배치되지 않은 씬에서는 null일 수 있다.</summary>
    public LandmarkManager LandmarkManager { get; }

    /// <summary>
    /// 건물 배치 복원의 id -> 프리팹 레지스트리. 캡처는 Building.PrefabId를 직접 읽으므로 쓰지 않는다.
    /// null이면 건물 복원만 건너뛴다.
    /// </summary>
    public BuildingCatalog BuildingCatalog { get; }

    /// <summary>새끼용이 없는 씬에서는 null일 수 있다. 그 경우 새끼용 타워만 복원되지 않는다.</summary>
    public BabyDragonPlacementCoordinator BabyDragonPlacementCoordinator { get; }

    /// <summary>
    /// 새 게임 +(뮤테이터) 서비스. 튜토리얼·테스트 씬에는 없으므로 null일 수 있고, 그때는
    /// 빈 목록 / 0점으로 캡처된다(= 표준 모드). 복원 쪽에서는 이 서비스가 뮤테이터를 되살린다.
    /// </summary>
    public RunModifierService RunModifierService { get; }

    public SaveCaptureContext(
        GameManager gameManager,
        CycleManager cycleManager,
        ResourceManager resourceManager,
        PopulationManager populationManager,
        ResearchManager researchManager,
        DragonTreeManager dragonTreeManager,
        ConquestManager conquestManager,
        GridMap gridMap,
        WaveCycleProgression waveCycleProgression,
        Castle castle,
        LandmarkManager landmarkManager,
        BuildingCatalog buildingCatalog,
        BabyDragonPlacementCoordinator babyDragonPlacementCoordinator,
        RunModifierService runModifierService)
    {
        GameManager = gameManager;
        CycleManager = cycleManager;
        ResourceManager = resourceManager;
        PopulationManager = populationManager;
        ResearchManager = researchManager;
        DragonTreeManager = dragonTreeManager;
        ConquestManager = conquestManager;
        GridMap = gridMap;
        WaveCycleProgression = waveCycleProgression;
        Castle = castle;
        LandmarkManager = landmarkManager;
        BuildingCatalog = buildingCatalog;
        BabyDragonPlacementCoordinator = babyDragonPlacementCoordinator;
        RunModifierService = runModifierService;
    }

    public bool IsValid => GameManager != null && CycleManager != null && GameManager.CurrentRun != null;
}
