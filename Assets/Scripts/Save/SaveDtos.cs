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
    public LandmarkStateDto Landmarks;
    public GuideQuestStateDto GuideQuests;

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

        // 랜드마크 도입 전에 저장된 슬롯에는 이 필드가 없다 - 빈 상태로 채워 하위 호환을 유지한다.
        Landmarks ??= new LandmarkStateDto();

        // 가이드 퀘스트 도입 전에 저장된 슬롯에도 이 필드가 없다 - 같은 이유로 빈 상태로 채운다.
        GuideQuests ??= new GuideQuestStateDto();

        Run.Normalize();
        Resources.Normalize();
        Population.Normalize();
        Research.Normalize();
        DragonTree.Normalize();
        Conquest.Normalize();
        Map.Normalize();
        Castle.Normalize();
        Landmarks.Normalize();
        GuideQuests.Normalize();

        // 건물의 새끼용 인덱스는 Run.Normalize가 인벤토리를 확정한 뒤에야 검증할 수 있다.
        Map.NormalizeBabyDragonReferences(Run.BabyDragons.Count);
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
    /// 저장 당시 타워에 설치돼 있었는지. 복원의 권위는 이 값이 아니라 MapStateDto.Buildings에 있다 -
    /// 실제로 새끼용 타워가 세워진 개체만 설치 상태가 된다(SaveRestore 참고).
    /// 이 필드는 그 결과와 대조해 어긋남을 경고하는 데 쓴다.
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

    // 건물별 배치 인구는 여기가 아니라 BuildingPlacementDto.AssignedPopulation에 있다 -
    // 할당이 건물 인스턴스에 종속이라 어느 건물의 몫인지 없이는 복원할 수 없기 때문이다.

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

/// <summary>
/// 랜드마크 진행 상태. 수령 이력과 배치 인구를 나눠 담는 이유는 복원 시점이 다르기 때문이다 -
/// 수령 이력은 영토 복원 "이전"에, 배치 인구는 "이후"에 적용해야 한다. SaveRestore.Apply 참고.
/// </summary>
public sealed class LandmarkStateDto
{
    /// <summary>보상을 이미 수령한 랜드마크의 LandmarkId.</summary>
    public List<string> ClaimedLandmarkIds;

    public List<LandmarkOperationDto> Operations;

    public void Normalize()
    {
        ClaimedLandmarkIds ??= new List<string>();
        ClaimedLandmarkIds.RemoveAll(string.IsNullOrWhiteSpace);

        Operations ??= new List<LandmarkOperationDto>();
        Operations.RemoveAll(operation =>
            operation == null || string.IsNullOrWhiteSpace(operation.LandmarkId));

        foreach (LandmarkOperationDto operation in Operations)
        {
            operation.Normalize();
        }
    }
}

public sealed class LandmarkOperationDto
{
    public string LandmarkId;
    public int AssignedPopulation;

    public void Normalize()
    {
        AssignedPopulation = Mathf.Max(0, AssignedPopulation);
    }
}

/// <summary>
/// 가이드 퀘스트 진행도. 랜드마크와 같은 방식으로 <b>필드 추가만</b> 하므로
/// SaveSchema.CURRENT_VERSION은 올리지 않는다 - 이 필드가 없던 세이브는 빈 상태로 접힌다(규약 2).
///
/// 가이드 <b>수준</b>은 여기 없다. 그것은 런이 아니라 플레이어 설정이라
/// SettingsService의 PlayerPrefs에 있다 - 새 게임을 시작해도 이어져야 하기 때문이다.
/// </summary>
public sealed class GuideQuestStateDto
{
    /// <summary>완료한 퀘스트의 QuestId.</summary>
    public List<string> CompletedQuestIds;

    /// <summary>조언자 카드에 이미 답했는가. 이 런에서 카드를 다시 띄우지 않는 기준이다.</summary>
    public bool IsIntroAnswered;

    public void Normalize()
    {
        CompletedQuestIds ??= new List<string>();
        CompletedQuestIds.RemoveAll(string.IsNullOrWhiteSpace);
    }
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

    /// <summary>
    /// 플레이어가 지은 건물의 배치. BuildingCatalog에 등록된(= Building.PrefabId가 채워진) 건물만 담는다 -
    /// 성·랜드마크·임시 방벽은 각자 재생성 경로가 따로 있어 대상이 아니다.
    /// </summary>
    public List<BuildingPlacementDto> Buildings;

    public void Normalize()
    {
        VisibleChunks ??= new List<Vector2IntDto>();
        ConqueredChunks ??= new List<Vector2IntDto>();
        EnhancedChunks ??= new List<Vector2IntDto>();
        Buildings ??= new List<BuildingPlacementDto>();

        // 프리팹을 못 찾거나 놓을 자리를 모르는 항목은 복원 코드가 어차피 버린다 - 여기서 미리 걷어낸다.
        Buildings.RemoveAll(building =>
            building == null || string.IsNullOrWhiteSpace(building.PrefabId) || building.Anchor == null);

        foreach (BuildingPlacementDto building in Buildings)
        {
            building.Normalize();
        }
    }

    /// <summary>
    /// 새끼용 인덱스가 실제 인벤토리를 가리키는지 확인한다. MapStateDto 혼자서는 판정할 수 없어
    /// (RunStateDto를 모른다) SaveGameDto.TryNormalize가 두 Normalize를 끝낸 뒤 불러 준다.
    /// </summary>
    public void NormalizeBabyDragonReferences(int babyDragonCount)
    {
        var claimedIndices = new HashSet<int>();

        foreach (BuildingPlacementDto building in Buildings)
        {
            if (building.BabyDragonIndex == BuildingPlacementDto.NO_BABY_DRAGON_INDEX)
            {
                continue;
            }

            // RunStateDto.Normalize의 DropUndefinedEnums가 항목을 버리면 인덱스가 밀린다.
            // 밀린 인덱스로 엉뚱한 개체를 결속하느니 결속을 포기하는 편이 낫다.
            bool isValid =
                building.BabyDragonIndex < babyDragonCount &&
                claimedIndices.Add(building.BabyDragonIndex);

            if (!isValid)
            {
                Debug.LogWarning(
                    $"[MapStateDto] 새끼용 인덱스 {building.BabyDragonIndex}가 유효하지 않아 결속을 해제합니다 " +
                    $"(보유 {babyDragonCount}마리).");

                building.BabyDragonIndex = BuildingPlacementDto.NO_BABY_DRAGON_INDEX;
            }
        }
    }
}

public sealed class BuildingPlacementDto
{
    /// <summary>새끼용 타워가 아니거나 결속할 레코드를 찾지 못했음.</summary>
    public const int NO_BABY_DRAGON_INDEX = -1;

    /// <summary>BuildingCatalog의 키(= Building.PrefabId). 비면 이 항목은 버려진다.</summary>
    public string PrefabId;

    public Vector3IntDto Anchor;
    public int RotationSteps;

    /// <summary>
    /// 이 건물에 배치돼 있던 인구. PopulationStateDto가 아니라 여기 있는 이유는,
    /// 할당이 건물 인스턴스에 종속이라 "어느 건물의 몫인지" 없이는 복원할 수 없기 때문이다.
    /// </summary>
    public int AssignedPopulation;

    /// <summary>건설된 주기(Building.ConstructedCycle). 철거 환급이 당일 건설 여부를 이 값으로 가른다.</summary>
    public int ConstructedCycle;

    /// <summary>
    /// 새끼용 타워일 때 RunStateDto.BabyDragons의 인덱스. 아니면 NO_BABY_DRAGON_INDEX.
    /// 속성만으로 찾으면 같은 속성 두 마리의 모드가 뒤바뀌므로(BabyDragonPlacementCoordinator 참고)
    /// 인덱스로 지목한다.
    /// </summary>
    public int BabyDragonIndex = NO_BABY_DRAGON_INDEX;

    public void Normalize()
    {
        RotationSteps =
            ((RotationSteps % Building.ROTATION_STEP_COUNT) + Building.ROTATION_STEP_COUNT)
            % Building.ROTATION_STEP_COUNT;

        AssignedPopulation = Mathf.Max(0, AssignedPopulation);
        ConstructedCycle = Mathf.Max(0, ConstructedCycle);

        if (BabyDragonIndex < 0)
        {
            BabyDragonIndex = NO_BABY_DRAGON_INDEX;
        }
    }
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
