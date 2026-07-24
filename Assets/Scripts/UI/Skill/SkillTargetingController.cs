using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 스킬 발동 흐름을 담당한다: 즉시 발동 / 지점 지정(광역) / 적 지정(단일).
/// 건설·점령 모드(BuildingPlacementController, ConquestModeController)와 동일한 입력 패턴
/// (InputActionReference Enable/Disable, 좌클릭 확정, 포인터-오버-UI 가드)을 따른다.
/// </summary>
public class SkillTargetingController : MonoBehaviour
{
    private const float ENEMY_PICK_RADIUS = 0.3f;
    // 스프라이트가 놓인 월드 Z 평면 - GetMouseWorldPoint가 이 평면 위의 지점을 구하는 데 사용한다.
    private const float TARGET_PLANE_WORLD_Z = 0f;

    [Tooltip("타겟 지정을 확정하는 액션 - 보통 좌클릭.")]
    [SerializeField] private InputActionReference _confirmAction;
    [Tooltip("타겟팅 모드를 취소하는 액션 - 보통 우클릭/ESC.")]
    [SerializeField] private InputActionReference _cancelAction;
    [Tooltip("스킬 발동자로 기록할 오브젝트. 비워두면 이 컨트롤러 자신.")]
    [SerializeField] private GameObject _caster;
    [Tooltip("GroundPoint 스킬 시전 중 커서를 따라다니며 실제 피해 범위를 보여줄 원형 인디케이터.")]
    [SerializeField] private RangeIndicator _rangeIndicator;
    [Tooltip("Enemy 스킬 시전 중 커서 아래 유효한 적 위에 띄울 작은 삼각형 인디케이터.")]
    [SerializeField] private SkillTargetIndicator _targetIndicator;

    private Camera _cam;
    private Skill _pendingSkill;
    private BaseMonster _hoveredEnemy;

    public bool IsTargeting => _pendingSkill != null;

    private GameObject CasterObject => _caster != null ? _caster : gameObject;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void OnEnable()
    {
        if (_confirmAction != null)
            _confirmAction.action.Enable();

        if (_cancelAction != null)
            _cancelAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_confirmAction != null)
            _confirmAction.action.Disable();

        if (_cancelAction != null)
            _cancelAction.action.Disable();
    }

    private void Update()
    {
        if (!IsTargeting)
            return;

        HandleCancelInput();

        if (!IsTargeting)
            return;

        UpdateRangeIndicator();
        UpdateHoveredEnemy();
        HandleConfirmInput();
    }

    /// <summary>HUD 스킬 아이콘 클릭 시 호출된다. 즉시 발동 스킬은 바로 적용하고,
    /// 지정이 필요한 스킬은 타겟팅 모드로 들어가 다음 좌클릭을 기다린다.</summary>
    public void BeginTargeting(Skill skill)
    {
        if (skill == null || !skill.CanUse)
            return;

        if (skill.Targeting == SkillTargeting.Instant)
        {
            skill.Activate(new SkillCastContext(Vector3.zero, null, CasterObject));
            return;
        }

        // 다른 스킬을 타겟팅하던 중 스킬을 바꿔 누르는 경우, 이전 인디케이터가 남아있지 않도록 먼저 정리한다.
        CancelTargeting();
        _pendingSkill = skill;

        if (skill.Targeting == SkillTargeting.GroundPoint && _rangeIndicator != null)
        {
            _rangeIndicator.Show(skill.AreaRadius, skill.AreaRadius * IsometricMath.RADIUS_Y_RATIO);
        }
    }

    public void CancelTargeting()
    {
        _pendingSkill = null;
        _hoveredEnemy = null;

        if (_rangeIndicator != null)
        {
            _rangeIndicator.Hide();
        }

        if (_targetIndicator != null)
        {
            _targetIndicator.Hide();
        }
    }

    // 타겟팅 중 GroundPoint 스킬이면 인디케이터를 커서 위치로 갱신한다.
    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null || _pendingSkill.Targeting != SkillTargeting.GroundPoint)
            return;

        _rangeIndicator.SetCenter(GetMouseWorldPoint());
    }

    // 타겟팅 중 Enemy 스킬이면 커서 아래 유효한 적을 찾아 _hoveredEnemy에 캐시하고 삼각형 인디케이터를 갱신한다.
    // HandleConfirmInput이 같은 프레임에 이 결과를 그대로 재사용해 확정한다.
    private void UpdateHoveredEnemy()
    {
        if (_pendingSkill.Targeting != SkillTargeting.Enemy)
            return;

        _hoveredEnemy = FindEnemyUnderPointer(GetMouseWorldPoint(), _pendingSkill.TargetLayers);

        if (_targetIndicator == null)
            return;

        if (_hoveredEnemy != null)
        {
            _targetIndicator.Show(_hoveredEnemy.TargetTransform.position);
        }
        else
        {
            _targetIndicator.Hide();
        }
    }

    private void HandleCancelInput()
    {
        if (_cancelAction != null && _cancelAction.action.WasPerformedThisFrame())
        {
            CancelTargeting();
        }
    }

    private void HandleConfirmInput()
    {
        if (_confirmAction == null || !_confirmAction.action.WasPerformedThisFrame())
            return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        Vector3 worldPoint = GetMouseWorldPoint();

        switch (_pendingSkill.Targeting)
        {
            case SkillTargeting.GroundPoint:
                ConfirmGroundPoint(worldPoint);
                break;

            case SkillTargeting.Enemy:
                ConfirmEnemy(worldPoint, _hoveredEnemy);
                break;
        }
    }

    private void ConfirmGroundPoint(Vector3 worldPoint)
    {
        Skill skill = _pendingSkill;
        CancelTargeting();
        skill.Activate(new SkillCastContext(worldPoint, null, CasterObject));
    }

    private void ConfirmEnemy(Vector3 worldPoint, BaseMonster target)
    {
        // 조준한 지점에 유효한 적이 없으면 타겟팅 모드를 유지한다 - 다시 겨눠서 확정할 수 있도록.
        if (target == null)
            return;

        Skill skill = _pendingSkill;
        CancelTargeting();
        skill.Activate(new SkillCastContext(worldPoint, target, CasterObject));
    }

    private Vector3 GetMouseWorldPoint()
    {
        if (_cam == null)
            _cam = Camera.main;

        if (_cam == null || Mouse.current == null)
            return Vector3.zero;

        Vector3 screenPos = Mouse.current.position.ReadValue();

        // 오쏘그래픽 카메라의 ScreenToWorldPoint z는 "카메라로부터의 거리"이지 월드 Z가 아니다.
        // 카메라 Z(예: -10)를 그대로 0으로 두면 카메라 자기 위치(니어클립 안쪽)가 나와
        // 물리 판정(2D라 Z 무관)엔 문제없지만 시각 요소(LineRenderer 등)는 화면에 보이지 않는다.
        screenPos.z = TARGET_PLANE_WORLD_Z - _cam.transform.position.z;
        return _cam.ScreenToWorldPoint(screenPos);
    }

    // 작은 적 콜라이더를 클릭으로 정확히 맞추기 어려우므로 반경을 두고 주운 뒤, 가장 가까운 적을 고른다.
    private static BaseMonster FindEnemyUnderPointer(Vector3 worldPoint, LayerMask layers)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPoint, ENEMY_PICK_RADIUS, layers);
        BaseMonster closest = null;
        float closestSqrDistance = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            BaseMonster monster = hit.GetComponentInParent<BaseMonster>();

            if (monster == null || monster.IsDead)
                continue;

            float sqrDistance = (monster.TargetTransform.position - worldPoint).sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = monster;
            }
        }

        return closest;
    }
}
