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
    // 배치 직후(아직 아침 정산 전)와 씬에 BabyDragonFeedingSystem이 연결되지 않은 경우
    // 모두 가동 상태로 시작한다 - 미연결이 "조용히 안 싸움"으로 나타나지 않게 하려는 기본값.
    private bool _isFed = true;

    // 이 인스턴스가 어느 인벤토리 레코드에서 왔는지 - 모드 저장/복원과 철거 시 반환 대상 판정에 쓴다.
    // 속성만으로 레코드를 되찾으면 같은 속성 두 마리가 서로 다른 모드일 때 뒤바뀔 수 있어 인스턴스 단위로 결속한다.
    public BabyDragon Record { get; private set; }

    protected CycleManager _cycleManager;

    // 새끼용은 건물이 아니라 인구로 가동하지 않는 설치물이라 이동 예산(연구 기반 일일 횟수)과 무관하다.
    protected override bool UsesMoveGrant => false;

    public override bool RequiresPopulation => false;

    public override int PopulationCapacity => 0;

    public BabyDragonData DragonData => Data as BabyDragonData;

    public bool CanOperate => _isFed;

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

    // 밤 이동 제한은 BuildingPlacementController.CanMoveNow(모든 건물 공통)가 담당한다.


    private void OnDisable()
    {
        _cycleManager.OnDayStart.RemoveListener(OnDayOrNightStart);
        _cycleManager.OnNightStart.RemoveListener(OnDayOrNightStart);
    }

    public void OnDayOrNightStart(int _)
    {
        Debug.Log(_isFed);
        _animator.SetBool(BROKEN_ANIM_KEY, !_isFed);
    }

    protected void Start()
    {
        var anim = GetComponent<Animator>();
        if(anim != null)
            anim.SetInteger(TYPE_ANIM_KEY, (int)DragonData.DragonType);
    }

    public void SetFed(bool isFed)
    {
        _isFed = isFed;
    }

    public void SetCycleManager(CycleManager cycleManager)
    {
        _cycleManager = cycleManager;
        _cycleManager.OnDayStart.AddListener(OnDayOrNightStart);
        _cycleManager.OnNightStart.AddListener(OnDayOrNightStart);
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
    }

    // 데이터상 그 모드를 쓸 수 없으면 무시한다(버튼도 같은 조건으로 비활성화되지만 방어적으로 한 번 더 확인).
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
