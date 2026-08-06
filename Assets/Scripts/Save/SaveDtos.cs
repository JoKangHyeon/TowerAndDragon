using System;
using System.Collections.Generic;
using UnityEngine;

// 세이브 파일에 기록되는 데이터 전송 객체(DTO) 모음.
// RunData.cs가 연관 타입 여러 개를 한 파일에 담은 선례를 따라 한 파일로 묶는다.
//
// [규약 1] enum은 정수 값으로 직렬화된다. 기존 멤버의 값을 바꾸거나 중간에 새 멤버를 끼워 넣으면
//          기존 세이브가 조용히 오염된다. 멤버 추가는 항상 enum 정의의 끝에만 한다.
// [규약 2] 필드를 삭제하거나 타입을 바꾸면 SaveSchema.CURRENT_VERSION을 올린다.
// [규약 3] 게임 클래스(RunData 등)를 직접 직렬화하지 않는다. 게임 클래스에 [JsonIgnore]를 붙이면
//          게임 코드가 세이브 시스템을 알게 되고, 필드를 추가할 때마다 세이브 포맷이 조용히 바뀐다.
//          DTO를 따로 두는 실질적 이득이 이것이므로, 이 파일 밖에 직렬화 속성을 두지 않는다.

/// <summary>세이브 파일 포맷의 버전. 로드 시 이 값으로 호환 여부를 먼저 판정한다.</summary>
public static class SaveSchema
{
    // v2: 스냅샷 시점이 "낮 시작 정산 직전"에서 "정산 이후 임의 시점"으로 바뀌었다(SaveService 계약 2).
    //     v1 파일은 정산 전 상태를 담고 있어 새 복원 경로로는 하루치 정산이 빠지므로 읽지 않는다.
    public const int CURRENT_VERSION = 2;

    /// <summary>읽을 수 있는 가장 낮은 버전. 마이그레이션을 구현하면 이 값을 낮춘다.</summary>
    public const int MIN_SUPPORTED_VERSION = 2;

    /// <summary>
    /// 이 빌드가 읽을 수 있는 버전인지. 슬롯 목록의 "손상됨" 판정과 본문 읽기의 버전 검사가
    /// 서로 다른 기준을 쓰지 않도록 판정을 여기 한곳에 모은다.
    /// </summary>
    public static bool IsSupportedVersion(int version) =>
        version >= MIN_SUPPORTED_VERSION && version <= CURRENT_VERSION;
}

public sealed class SaveGameDto
{
    public SaveMetaDto Meta;
    public RunStateDto Run;
    public ResourceStateDto Resources;
    public PopulationStateDto Population;
    public ProgressionStateDto Research;
    public ProgressionStateDto DragonTree;
    public ConquestStateDto Conquest;
    public MapStateDto Map;
    public CastleStateDto Castle;

    /// <summary>
    /// 복원 착수 전에 부르는 유일한 검증 지점. 한 번 복원을 시작하면 여러 매니저에 이미 쓴 뒤라
    /// 롤백이 불가능하므로, 여기서 통과하지 못한 세이브는 아예 적용하지 않는다.
    /// null 컬렉션을 빈 리스트로 정규화하므로 이 뒤의 복원 코드는 null 검사를 하지 않아도 된다.
    /// </summary>
    public bool TryNormalize()
    {
        if (Meta == null || Run == null)
        {
            Debug.LogError("[SaveGameDto] Meta 또는 Run이 비어 있어 복원할 수 없습니다.");
            return false;
        }

        if (Run.CurrentCycle < SaveValidation.FIRST_DAY_NUMBER)
        {
            Debug.LogError($"[SaveGameDto] 잘못된 일차: {Run.CurrentCycle}");
            return false;
        }

        // 밤 스냅샷은 웨이브 진행이 저장 대상이 아니라 이어서 시작할 수 없다.
        // CanSave가 낮만 허용하므로 정상 경로에서는 걸리지 않고, 손으로 고친 파일만 여기서 막힌다.
        if (Run.CyclePhase != (int)CycleManager.CycleState.Day)
        {
            Debug.LogError($"[SaveGameDto] 낮이 아닌 시점의 세이브는 복원할 수 없습니다: {Run.CyclePhase}");
            return false;
        }

        Resources ??= new ResourceStateDto();
        Population ??= new PopulationStateDto();
        Research ??= new ProgressionStateDto();
        DragonTree ??= new ProgressionStateDto();
        Conquest ??= new ConquestStateDto();
        Map ??= new MapStateDto();
        Castle ??= new CastleStateDto();

        Run.Normalize();
        Resources.Normalize();
        Population.Normalize();
        Research.Normalize();
        DragonTree.Normalize();
        Conquest.Normalize();
        Map.Normalize();
        Castle.Normalize();
        return true;
    }
}

public sealed class SaveMetaDto
{
    public int SchemaVersion;

    /// <summary>저장 당시의 Application.version. 표시·제보용이며 호환 판정에는 쓰지 않는다.</summary>
    public string GameVersion;

    /// <summary>
    /// 마지막 저장 시각(UTC). 슬롯 목록 UI가 "마지막 저장: ..."으로 표시한다.
    /// string이 아니라 DateTimeOffset인 이유는 SaveTimestampFormatter 주석 참고.
    /// </summary>
    public DateTimeOffset SavedAtUtc;

    public int SlotIndex;
    public bool IsAutoSave;

    /// <summary>누적 일차(RunData.CurrentCycle). 복원의 기준값이자 슬롯 목록 표시값.</summary>
    public int DayNumber;

    /// <summary>표시 전용. 일차에서 파생되므로 복원에는 쓰지 않는다.</summary>
    public int CycleNumber;

    /// <summary>슬롯 아이콘 표시용 어미용 속성.</summary>
    public int DragonType;

    /// <summary>
    /// 슬롯 목록에 자원 보유량을 띄우기 위한 표시 전용 사본(본문 Resources와 같은 값).
    /// 복원은 항상 save.json의 ResourceStateDto를 쓰므로 이 값은 읽지 않는다 -
    /// meta.json만 읽는 슬롯 목록이 본문을 열지 않고도 자원을 그릴 수 있게 하는 것이 유일한 목적이다.
    /// </summary>
    public List<ResourceAmountDto> Resources;
}

public sealed class RunStateDto
{
    /// <summary>누적 일차. 주기·웨이브·연구 티어·포탈 개방은 전부 여기서 파생된다.</summary>
    public int CurrentCycle;

    /// <summary>
    /// 저장 당시의 낮/밤(CycleManager.CycleState). CurrentCycle(누적 일차)과 다른 값이다.
    /// 복원은 Day만 받아들인다 - 밤 스냅샷은 웨이브 진행이 빠져 있어 이어서 시작할 수 없다.
    /// </summary>
    public int CyclePhase;

    public int DragonType;
    public List<BabyDragonDto> BabyDragons;
    public List<DragonEggDto> DragonEggs;

    /// <summary>
    /// 주기 보스 처치 보상으로 알을 이미 받은 주기 목록. 인벤토리와 별개로 저장해야
    /// 불러오기 후에도 "주기당 1회" 중복 지급 방지가 유지된다(RunData.HasClaimedBossDragonEggReward).
    /// </summary>
    public List<BossDragonEggRewardDto> BossDragonEggRewards;

    public void Normalize()
    {
        BabyDragons ??= new List<BabyDragonDto>();
        DragonEggs ??= new List<DragonEggDto>();

        // 이 필드가 없던 구버전 세이브는 null로 들어온다 - 빈 목록이면 기존 동작 그대로다.
        BossDragonEggRewards ??= new List<BossDragonEggRewardDto>();

        DragonType = SaveValidation.CoerceDefinedEnum(
            DragonType, typeof(DragonType), SaveValidation.DEFAULT_DRAGON_TYPE);

        SaveValidation.DropUndefinedEnums(BabyDragons, dto => dto.DragonType, typeof(DragonType));
        SaveValidation.DropUndefinedEnums(DragonEggs, dto => dto.DragonType, typeof(DragonType));
        SaveValidation.DropUndefinedEnums(
            BossDragonEggRewards, dto => dto.DragonType, typeof(DragonType));
        SaveValidation.DropInvalidCycleNumbers(
            BossDragonEggRewards, dto => dto.CycleNumber);

        foreach (BabyDragonDto babyDragon in BabyDragons)
        {
            babyDragon.Mode = SaveValidation.CoerceDefinedEnum(
                babyDragon.Mode, typeof(BabyDragonMode), SaveValidation.DEFAULT_BABY_DRAGON_MODE);
        }

        foreach (DragonEggDto egg in DragonEggs)
        {
            egg.FedDayCount = Mathf.Max(0, egg.FedDayCount);
        }
    }
}

public sealed class BabyDragonDto
{
    public string DragonName;
    public int DragonType;

    /// <summary>
    /// 저장 당시 타워에 설치돼 있었는지. 건물 배치 복원이 구현되기 전까지는 복원 시 무조건
    /// false로 강제한다(SaveRestore 참고) - 타워가 없는데 설치됐다고 주장하는 상태를 막는다.
    /// </summary>
    public bool IsInTower;

    public int Mode;
    public bool IsModeInitialized;
}

public sealed class DragonEggDto
{
    public int DragonType;
    public int FedDayCount;
}

public sealed class BossDragonEggRewardDto
{
    public int CycleNumber;
    public int DragonType;
}

public sealed class CastleStateDto
{
    /// <summary>
    /// 저장 당시 성의 현재 체력. 최대 체력은 프리팹·연구가 정하므로 저장하지 않고
    /// 복원 시 살아 있는 최대치로 클램프한다. 0 이하는 "기록 없음"으로 보고 복원을 건너뛴다.
    /// </summary>
    public float CurrentHealth;

    public void Normalize() => CurrentHealth = Mathf.Max(0f, CurrentHealth);
}

public sealed class ResourceStateDto
{
    /// <summary>
    /// Dictionary가 아니라 List인 이유: Newtonsoft는 Dictionary 키를 ToString()하는데,
    /// ResourceType이 [Flags]라 키가 "Food, Wood" 같은 형태가 될 수 있고 enum 이름 변경에 취약하다.
    /// </summary>
    public List<ResourceAmountDto> Amounts;

    public void Normalize()
    {
        Amounts ??= new List<ResourceAmountDto>();
        SaveValidation.DropUndefinedEnums(Amounts, dto => dto.Type, typeof(ResourceType));

        foreach (ResourceAmountDto amount in Amounts)
        {
            amount.Amount = Mathf.Max(0, amount.Amount);
        }
    }
}

public sealed class ResourceAmountDto
{
    public int Type;
    public int Amount;
}

public sealed class PopulationStateDto
{
    public int MaxPopulation;

    // TODO(범위 밖): PopulationAllocation은 건물 인스턴스에 종속돼 있어 저장할 수 없다.
    //   BuildingPlacementDto가 실제로 구현될 때 그 안으로 들어간다.

    public void Normalize()
    {
        MaxPopulation = Mathf.Max(0, MaxPopulation);
    }
}

/// <summary>연구 트리와 용 스킬트리가 공유하는 형태. 용 스킬트리는 ResearchPoints를 쓰지 않는다.</summary>
public sealed class ProgressionStateDto
{
    public List<string> UnlockedNodeIds;
    public int ResearchPoints;

    public void Normalize()
    {
        UnlockedNodeIds ??= new List<string>();
        UnlockedNodeIds.RemoveAll(string.IsNullOrWhiteSpace);
        ResearchPoints = Mathf.Max(0, ResearchPoints);
    }
}

public sealed class ConquestStateDto
{
    public List<ConquestExpeditionDto> ActiveExpeditions;

    public void Normalize()
    {
        ActiveExpeditions ??= new List<ConquestExpeditionDto>();

        foreach (ConquestExpeditionDto expedition in ActiveExpeditions)
        {
            expedition.Normalize();
        }
    }
}

public sealed class ConquestExpeditionDto
{
    public Vector2IntDto TargetChunkCoord;
    public ResourceCostDto Cost;

    /// <summary>
    /// 저장값을 그대로 쓴다. 복원 시 재계산하면 원정 발송 이후 해금한 연구의 기간 감소가
    /// 소급 적용돼 원본 런과 달라진다.
    /// </summary>
    public int DaysRequired;

    public int DaysProgressed;

    public void Normalize()
    {
        TargetChunkCoord ??= new Vector2IntDto();
        Cost ??= new ResourceCostDto();
        DaysRequired = Mathf.Max(1, DaysRequired);
        DaysProgressed = Mathf.Clamp(DaysProgressed, 0, DaysRequired);
    }
}

public sealed class ResourceCostDto
{
    public int Population;
    public int Food;
    public int Wood;
    public int Stone;

    public ResourceCost ToResourceCost() => new ResourceCost
    {
        Population = Population,
        Food = Food,
        Wood = Wood,
        Stone = Stone,
    };

    public static ResourceCostDto From(ResourceCost cost) => new ResourceCostDto
    {
        Population = cost.Population,
        Food = cost.Food,
        Wood = cost.Wood,
        Stone = cost.Stone,
    };
}

public sealed class MapStateDto
{
    public List<Vector2IntDto> VisibleChunks;
    public List<Vector2IntDto> ConqueredChunks;

    /// <summary>
    /// EnemyEnhancementManager.ApplyProfile을 실제로 태운 청크. 점령 청크와 다르다 -
    /// 성의 홈 청크는 점령 상태이지만 강화를 적용하지 않는다(ConquestManager 참고).
    /// </summary>
    public List<Vector2IntDto> EnhancedChunks;

    // TODO(범위 밖 - 건물 배치 복원). 지금은 항상 빈 리스트다.
    //  막힌 지점: GridMap._buildingFootprintCells의 키가 Building MonoBehaviour이고,
    //    RegisterFootprint/ConstructBuilding이 anchor를 받지만 어디에도 보관하지 않는다.
    //    즉 "이 건물이 어느 앵커에 어느 회전으로 놓였는가"를 아는 곳이 프로젝트에 없다.
    //  선행 작업:
    //   (1) Building에 안정적인 PrefabId 부여 + BuildingCatalog에 id -> prefab 조회 추가
    //       (현재 GetPrefab<T>()는 타입 기반이라 같은 타입의 여러 종을 구분하지 못한다)
    //   (2) Building 또는 GridMap이 anchor(Vector3Int)와 rotationSteps를 보관
    //   (3) 복원 시 GridMap.ConstructBuilding 재호출 + 인구 할당 재생성
    //       + BabyDragonTower.BindRecord 재바인딩 + BabyDragon.IsInTower 복구
    //  복원 순서상 삽입 위치: 청크 상태 복원 이후, 원정 복원과 StartDay 이전
    //    (CanConstructBuilding이 점령 상태를 요구하고, 건물 인구가 원정 인구와 총량을 다툰다)
    public List<BuildingPlacementDto> Buildings;

    public void Normalize()
    {
        VisibleChunks ??= new List<Vector2IntDto>();
        ConqueredChunks ??= new List<Vector2IntDto>();
        EnhancedChunks ??= new List<Vector2IntDto>();
        Buildings ??= new List<BuildingPlacementDto>();
    }
}

public sealed class BuildingPlacementDto
{
    public string PrefabId;
    public Vector3IntDto Anchor;
    public int RotationSteps;
}

/// <summary>
/// Unity의 Vector2Int를 직접 직렬화하지 않는다. Unity 벡터 구조체는 계산 프로퍼티까지 public이라
/// 잡동사니가 딸려오고(Vector3.normalized는 self-referencing loop로 알려진 이슈),
/// 커스텀 JsonConverter로 문자열 인코딩을 하면 파싱용 문자열 리터럴이 생긴다.
/// </summary>
public sealed class Vector2IntDto
{
    public int X;
    public int Y;

    public Vector2Int ToVector2Int() => new Vector2Int(X, Y);

    public static Vector2IntDto From(Vector2Int value) => new Vector2IntDto { X = value.x, Y = value.y };

    public static List<Vector2IntDto> From(IEnumerable<Vector2Int> values)
    {
        var result = new List<Vector2IntDto>();

        foreach (Vector2Int value in values)
        {
            result.Add(From(value));
        }

        return result;
    }

    public static List<Vector2Int> ToVector2IntList(IEnumerable<Vector2IntDto> values)
    {
        var result = new List<Vector2Int>();

        foreach (Vector2IntDto value in values)
        {
            if (value != null)
            {
                result.Add(value.ToVector2Int());
            }
        }

        return result;
    }
}

/// <summary>건물 앵커용. <see cref="Vector2IntDto"/>와 같은 이유로 Unity 타입을 직접 쓰지 않는다.</summary>
public sealed class Vector3IntDto
{
    public int X;
    public int Y;
    public int Z;

    public Vector3Int ToVector3Int() => new Vector3Int(X, Y, Z);

    public static Vector3IntDto From(Vector3Int value) =>
        new Vector3IntDto { X = value.x, Y = value.y, Z = value.z };
}

/// <summary>DTO 정규화가 공유하는 검증 헬퍼.</summary>
public static class SaveValidation
{
    /// <summary>게임은 1일차부터 시작한다(CycleManager.StartDay가 0에서 1로 올린다).</summary>
    public const int FIRST_DAY_NUMBER = 1;

    // 대체값을 RunStateDto/BabyDragonDto 안에 두지 않는 이유: 그 DTO들의 필드명(DragonType, Mode)이
    // enum 타입명을 가려 DragonType.Ice 같은 멤버 접근이 성립하지 않는다.
    public static readonly int DEFAULT_DRAGON_TYPE = (int)DragonType.Ice;
    public static readonly int DEFAULT_BABY_DRAGON_MODE = (int)BabyDragonMode.Attack;

    /// <summary>
    /// enum으로 정의되지 않은 정수값을 가진 항목을 버린다. 밸런싱으로 enum 멤버가 삭제된
    /// 구버전 세이브를 로드할 때 정의되지 않은 값이 그대로 흘러 들어가는 것을 막는다.
    /// </summary>
    public static void DropUndefinedEnums<T>(List<T> items, Func<T, int> selectValue, Type enumType)
    {
        items.RemoveAll(item =>
        {
            if (item == null)
            {
                return true;
            }

            int value = selectValue(item);
            bool isDefined = Enum.IsDefined(enumType, value);

            if (!isDefined)
            {
                Debug.LogWarning($"[SaveValidation] 정의되지 않은 {enumType.Name} 값 {value} - 항목을 버립니다.");
            }

            return !isDefined;
        });
    }

    /// <summary>
    /// 주기 번호가 유효 범위(WaveCycleRules) 밖인 항목을 버린다. 주기 수가 줄어든
    /// 구버전 세이브나 손댄 파일이 존재하지 않는 주기의 기록을 들고 오는 것을 막는다.
    /// </summary>
    public static void DropInvalidCycleNumbers<T>(List<T> items, Func<T, int> selectCycleNumber)
    {
        items.RemoveAll(item =>
        {
            if (item == null)
            {
                return true;
            }

            int cycleNumber = selectCycleNumber(item);
            bool isValid = WaveCycleRules.IsValidCycleNumber(cycleNumber);

            if (!isValid)
            {
                Debug.LogWarning($"[SaveValidation] 유효하지 않은 주기 번호 {cycleNumber} - 항목을 버립니다.");
            }

            return !isValid;
        });
    }

    /// <summary>
    /// enum으로 정의되지 않은 스칼라 값을 대체값으로 되돌린다. 컬렉션과 달리 버릴 수 없으므로
    /// 항목을 제거하는 <see cref="DropUndefinedEnums{T}"/> 대신 이쪽을 쓴다.
    /// </summary>
    public static int CoerceDefinedEnum(int value, Type enumType, int fallbackValue)
    {
        if (Enum.IsDefined(enumType, value))
        {
            return value;
        }

        Debug.LogWarning(
            $"[SaveValidation] 정의되지 않은 {enumType.Name} 값 {value} - {fallbackValue}로 대체합니다.");

        return fallbackValue;
    }
}
