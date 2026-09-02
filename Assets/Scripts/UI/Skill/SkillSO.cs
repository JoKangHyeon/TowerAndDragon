using UnityEngine;

[CreateAssetMenu(fileName = "New Skill SO", menuName = "Skill/SkillSO")]
public class SkillSO:ScriptableObject
{
    public SkillType Type;
    public string NameStringKey;
    public string DescriptionStringKey;
    public Sprite Sprite;

    public float DefaultCooltime;
    public int DefaultUsePerDay;

    [Header("체력비례 스킬 수치 - 현재 체력 기준 데미지")]
    [Range(0f, 1f)]
    public float DamagePercentOfCurrentHealth;

    [Tooltip("현재 체력 비례가 아닌 고정 피해량(메테오 등). 0이면 사용하지 않는다.")]
    [Min(0f)]
    public float FlatDamage;
    [Tooltip("광역 스킬의 적용 반경.")]
    public float AreaRadius;
    [Tooltip("타겟팅 시 인식할 레이어 - 보통 Enemy 레이어.")]
    public LayerMask TargetLayers;

    [Header("용 스킬트리 액티브 - 상태이상 부여형")]
    [Tooltip("발동 시 대상에 부여할 상태이상(빙결·화상 재부여 등). 필요 없는 스킬은 비워 둔다.")]
    public StatusEffectSO AppliedStatus;

    [Header("용 스킬트리 액티브 - 회복형 수치")]
    [Tooltip("발동 시 성에 즉시 회복시킬 체력량. 회복형이 아닌 스킬은 사용하지 않는다.")]
    public float HealAmount;

    [Header("방벽 설치 스킬 전용")]
    [Tooltip("설치될 방벽 프리팹 (StoneBarricade 컴포넌트 포함)")]
    public GameObject BarricadePrefab;

    // 설치 충격은 방벽 하나당 한 번만 뜬다. TargetVfxLifetimeSeconds와 따로 두는 이유는
    // 그쪽이 "피해를 입은 몬스터마다" 띄우는 값이라 성격이 다르기 때문이다.
    private const float DEFAULT_BARRICADE_PLACEMENT_VFX_LIFETIME_SECONDS = 1.2f;

    [Header("방벽 연출")]
    [Tooltip("방벽이 설치될 자리를 가리키며 타게팅 내내 떠 있는 바닥 마커. 비우면 생략한다. " +
        "⚠️ 루프 재생 · 시뮬레이션 공간 Local인 프리팹이어야 한다 - 1회성 이펙트를 넣으면 " +
        "첫 프레임에만 보이고, World 공간이면 파티클이 커서 뒤로 끌린다.")]
    [WiringOptional]
    public GameObject BarricadePreviewMoveVfxPrefab;

    [Tooltip("방벽 설치가 실제로 성공한 지점에서 1회 재생할 낙하·충격 연출. 비우면 생략한다. " +
        "⚠️ TargetVfxPrefab을 재사용하지 말 것 - 그쪽은 범위 안 몬스터마다 뜨는 피격 임팩트다.")]
    [WiringOptional]
    public GameObject BarricadePlacementVfxPrefab;

    [Tooltip("위 설치 연출을 띄워 둘 시간(초).")]
    [Min(0f)]
    public float BarricadePlacementVfxLifetimeSeconds =
        DEFAULT_BARRICADE_PLACEMENT_VFX_LIFETIME_SECONDS;

    // 새끼용 회복 파동(TA_BD_TowerHealEffect)이 쓰는 값과 같게 뒀다 - 같은 프리팹을 재사용하므로
    // 재생 길이도 같아야 화면에서 같은 연출로 읽힌다.
    private const float DEFAULT_TARGET_VFX_LIFETIME_SECONDS = 1.1f;

    [Header("시전 연출 - 대상별(B계층)")]
    [Tooltip("스킬이 실제로 효과를 준 대상 위에 재생할 이펙트. 비우면 생략한다. " +
        "화면 전체에 깔리는 배경 오버레이(A계층)는 이것과 별개로 SkillCastOverlayHost가 담당한다.")]
    [WiringOptional]
    public GameObject TargetVfxPrefab;

    [Tooltip("위 이펙트를 띄워 둘 시간(초).")]
    [Min(0f)]
    public float TargetVfxLifetimeSeconds = DEFAULT_TARGET_VFX_LIFETIME_SECONDS;

    public Skill GetSkill()
    {
        switch (Type)
        {
            case SkillType.DEBUG:
                return new DebugSkill(this);
            case SkillType.SINGLE_CURRENT_HEALTH_DAMAGE:
                return new SingleCurrentHealthDamageSkill(this);
            case SkillType.AREA_CURRENT_HEALTH_DAMAGE:
                return new AreaCurrentHealthDamageSkill(this);
            case SkillType.FREEZE_ALL:
                return new FreezeAllSkill(this);
            case SkillType.GLOBAL_CURRENT_HEALTH_DAMAGE:
                return new GlobalCurrentHealthDamageSkill(this);
            case SkillType.REPAIR_TOWERS:
                return new RepairTowersSkill(this);
            case SkillType.HEAL_CASTLE:
                return new HealCastleSkill(this);
            case SkillType.METEOR_BARRICADE:
                return new MeteorBarricadeSkill(this);
            default:
                return null;
        }
    }
}
