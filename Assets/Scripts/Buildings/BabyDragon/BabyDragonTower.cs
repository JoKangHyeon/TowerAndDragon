using UnityEngine;

/// <summary>
/// 설치형 구조물인 새끼용. 공격은 일반 타워와 동일한 TowerAttack이 담당하고,
/// 가동 조건만 인구 대신 슬라임 먹이로 바꾼다(BabyDragonFeedingSystem이 매일 아침 지불).
/// Tower.Awake/Start가 private이므로 여기서 Awake/Start를 선언하면 안 된다.
/// </summary>
public class BabyDragonTower : Tower, ITowerStaffing
{
    // 배치 직후(아직 아침 정산 전)와 씬에 BabyDragonFeedingSystem이 연결되지 않은 경우
    // 모두 가동 상태로 시작한다 - 미연결이 "조용히 안 싸움"으로 나타나지 않게 하려는 기본값.
    private bool _isFed = true;

    public override bool RequiresPopulation => false;

    public override int PopulationCapacity => 0;

    public BabyDragonData DragonData => Data as BabyDragonData;

    public bool CanOperate => _isFed;

    // 먹이는 가동률에 관여하지 않는다 - 먹었으면 기준 속도, 못 먹었으면 CanOperate로 정지.
    public float StaffingRatio => 1f;

    private static readonly int TYPE_ANIM_KEY = Animator.StringToHash("Type");


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
}
