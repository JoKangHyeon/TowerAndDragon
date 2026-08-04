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
            Run = CaptureRun(run),
            Resources = CaptureResources(context.ResourceManager),
            Population = CapturePopulation(context.PopulationManager),
            Research = CaptureResearch(context.ResearchManager),
            DragonTree = CaptureDragonTree(context.DragonTreeManager),
            Conquest = CaptureConquest(context.ConquestManager),
            Map = CaptureMap(context.ConquestManager, context.GridMap),
        };

        dto.Meta = CaptureMeta(context, dto.Run, slotIndex, isAutoSave);
        return dto;
    }

    private static SaveMetaDto CaptureMeta(
        SaveCaptureContext context,
        RunStateDto run,
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
            CycleNumber = context.WaveCycleProgression != null
                ? context.WaveCycleProgression.CurrentCycleNumber
                : 0,
            DragonType = run.DragonType,
        };
    }

    private static RunStateDto CaptureRun(RunData run)
    {
        var dto = new RunStateDto
        {
            CurrentCycle = run.CurrentCycle,
            DragonType = run.CurrentDragon != null ? (int)run.CurrentDragon.CurrentType : 0,
            BabyDragons = new List<BabyDragonDto>(),
            DragonEggs = new List<DragonEggDto>(),
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

        return dto;
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

    private static MapStateDto CaptureMap(ConquestManager conquestManager, GridMap gridMap)
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

        // TODO(범위 밖): 건물 배치. MapStateDto.Buildings의 주석 참고.
        return dto;
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

    public SaveCaptureContext(
        GameManager gameManager,
        CycleManager cycleManager,
        ResourceManager resourceManager,
        PopulationManager populationManager,
        ResearchManager researchManager,
        DragonTreeManager dragonTreeManager,
        ConquestManager conquestManager,
        GridMap gridMap,
        WaveCycleProgression waveCycleProgression)
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
    }

    public bool IsValid => GameManager != null && CycleManager != null && GameManager.CurrentRun != null;
}
