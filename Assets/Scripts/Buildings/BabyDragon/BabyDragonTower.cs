using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 설치형 구조물인 새끼용. 공격은 일반 타워와 동일한 TowerAttack이 담당하고,
/// 가동 조건만 인구 대신 슬라임 먹이로 바꾼다(BabyDragonFeedingSystem이 매일 아침 지불).
/// 버프/공격 모드(기획 §10 "전투형 or 버프형 중 선택")는 RunData의 BabyDragon 레코드에
/// 저장해 철거 후 재설치해도 유지되게 한다 - BabyDragonPlacementCoordinator.BindRecord가 배치 시 연결한다.
/// Tower.Start가 private이므로 여기서 Start를 선언하면 안 된다(Awake는 이제 protected override라 안전).
/// </summary>
public class BabyDragonTower : Tower, ITowerStaffing
{
    // 이 인스턴스가 어느 인벤토리 레코드에서 왔는지 - 모드 저장/복원과 철거 시 반환 대상 판정에 쓴다.
    // 속성만으로 레코드를 되찾으면 같은 속성 두 마리가 서로 다른 모드일 때 뒤바뀔 수 있어 인스턴스 단위로 결속한다.
    public BabyDragon Record { get; private set; }

    // 새끼용은 건물이 아니라 인구로 가동하지 않는 설치물이라 이동 예산(연구 기반 일일 횟수)과 무관하다.
    protected override bool UsesMoveGrant => false;

    public override bool RequiresPopulation => false;
    public override MonsterTargetType BaseTargetType => MonsterTargetType.Dragon;
    public override int PopulationCapacity => 0;

    public BabyDragonData DragonData => Data as BabyDragonData;

    // 굶주림은 인스턴스가 아니라 보유 레코드(RunData.BabyDragon)에 있다 - 철거 후 재설치해도
    // 유지되게 하려는 것이다(Mode와 같은 취급). Record가 없으면(에디터에서 직접 놓은 개체)
    // 기존 기본값과 같게 가동 중으로 본다.
    public bool CanOperate => Record == null || Record.IsFed;

    //시간 새끼용은 공격 모드일 때만 밤에도 이동 가능하다 (버프 모드일땐 밤에도 고정)
    public override bool CanMoveAtNight =>
        DragonData != null &&
        DragonData.DragonType == DragonType.Time &&
        Mode == BabyDragonMode.Attack &&
        CanOperate;

    // 먹이는 가동률에 관여하지 않는다 - 먹었으면 기준 속도, 못 먹었으면 CanOperate로 정지.
    public float StaffingRatio => 1f;

    public BabyDragonMode Mode => Record != null ? Record.Mode : BabyDragonMode.Attack;
    public bool CanUseAttackMode =>
        DragonData != null &&
        DragonData.CanAttack;
    public bool CanUseBuffMode =>
        DragonData != null && 
        (DragonData.BuffRadius > 0f ||
        DragonData.HasTowerAura);

    // 버프모드일 때는 공격을 멈춘다 - TowerAttack.SetAttackEnabled가 이미 CanAttack(공격 데이터 유무)과
    // AND하므로, 공격 데이터가 아예 없는 개체(예: 생명 속성)에는 이 훅이 영향을 주지 않는다.
    protected override bool CanAttackInCurrentMode => Mode == BabyDragonMode.Attack;

    // 모드가 바뀔 때마다 발화 - BabyDragonBuffSystem이 구독해 버프 재계산 시점을 놓치지 않게 한다.
    public UnityEvent ModeChanged = new();

    private static readonly int TYPE_ANIM_KEY = Animator.StringToHash("Type");

    // 굶으면 공격/버프가 멈추므로(CanOperate) 체력 0으로 멈춘 타워와 같은 쓰러진 자세로 보여준다.
    // 두 사유가 겹칠 수 있어(굶은 채로 파괴) Tower가 OR로 합쳐 판정한다 - 아침에 먹이를 줘도
    // 아직 부활 대기 중이면 쓰러진 자세가 유지된다.
    protected override bool IsBrokenPose => base.IsBrokenPose || !CanOperate;

    // 밤 이동 제한은 BuildingPlacementController.CanMoveNow(모든 건물 공통)가 담당한다.
    protected void Start()
    {
        var anim = GetComponent<Animator>();
        if(anim != null)
            anim.SetInteger(TYPE_ANIM_KEY, (int)DragonData.DragonType);

        // 아침 정산(SetFed)이 Start보다 먼저 돌았을 수 있다 - 속성과 함께 자세도 한 번 맞춰 둔다.
        RefreshBrokenAnimation();
    }

    public void SetFed(bool isFed)
    {
        if (Record != null)
        {
            Record.IsFed = isFed;
        }

        RefreshBrokenAnimation();
    }

    // BabyDragonPlacementCoordinator.HandleBuildingAdded가 배치 시 호출한다.
    // 레코드에 아직 모드가 정해지지 않았다면(IsModeInitialized == false), 데이터 기반 기본값을 채운다 -
    // 공격 수단이 없는 개체(예: 생명 속성)가 기본 Attack 모드로 아무것도 못 하는 상황을 막기 위함.
    public void BindRecord(BabyDragon record)
    {
        Record = record;

        if (record != null && !record.IsModeInitialized)
        {
            record.Mode = CanUseAttackMode ? BabyDragonMode.Attack : BabyDragonMode.Buff;
            record.IsModeInitialized = true;
        }

        // Record가 붙기 전의 Mode는 기본값 Attack이고, Setup()이 그 상태로 이미 공격을 켜 두었다 -
        // 여기서 다시 확정하지 않으면 버프모드 레코드(철거 후 재설치·세이브 로드)가 공격까지 하는
        // 이중 상태가 된다. Setup보다 먼저 호출되더라도 _isInitialized 가드로 무해하고,
        // 그 경우는 뒤이은 Setup의 RefreshAttackEnabled가 같은 결론을 낸다.
        RefreshAttackEnabled();

        // 버프 재계산·UI도 같은 이유로 갱신이 필요하다. BabyDragonBuffSystem이 아직 이 인스턴스를
        // 등록하지 않았다면 리스너가 없어 무해하며, 등록 직후의 RecomputeAll이 바인딩된 모드를 본다.
        ModeChanged.Invoke();

        // CanOperate가 Record.IsFed를 참조하므로, 레코드가 붙는 순간(배치·세이브 복원) 굶주린
        // 채였던 개체가 곧바로 쓰러진 자세로 보여야 한다 - Start보다 먼저 불릴 수 있어 여기서도 맞춘다.
        RefreshBrokenAnimation();
    }

    // 데이터상 그 모드를 쓸 수 없으면 무시한다(버튼도 같은 조건으로 비활성화되지만 방어적으로 한 번 더 확인).
    //
    // 낮/밤 판정은 여기서 하지 않는다 - Building/Tower는 CycleManager를 모르고, "낮에만 되는 조작"은
    // BuildingPlacementController(IsDayForBuildActions·CanMoveNow·CanRemoveNow)와 그 UI 소비자가 판정하는
    // 것이 이 프로젝트의 구조다. 모드 잠금(이슈 173)도 같은 자리인 UI_BabyDragonManageWindow가 담당한다.
    // 새 호출부를 추가할 때는 그쪽에서 낮 여부를 확인해야 한다(현재 호출부는 그 창뿐).
    public void SetMode(BabyDragonMode mode)
    {
        if (Record == null || Record.Mode == mode)
        {
            return;
        }

        if (mode == BabyDragonMode.Attack && !CanUseAttackMode)
        {
            return;
        }

        if (mode == BabyDragonMode.Buff && !CanUseBuffMode)
        {
            return;
        }

        Record.Mode = mode;
        Record.IsModeInitialized = true;
        RefreshAttackEnabled();
        ModeChanged.Invoke();
    }
}
