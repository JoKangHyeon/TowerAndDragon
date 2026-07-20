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

    [Tooltip("타겟 지정을 확정하는 액션 - 보통 좌클릭.")]
    [SerializeField] private InputActionReference _confirmAction;
    [Tooltip("타겟팅 모드를 취소하는 액션 - 보통 우클릭/ESC.")]
    [SerializeField] private InputActionReference _cancelAction;
    [Tooltip("스킬 발동자로 기록할 오브젝트. 비워두면 이 컨트롤러 자신.")]
    [SerializeField] private GameObject _caster;

    private Camera _cam;
    private Skill _pendingSkill;

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

        if (IsTargeting)
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

        _pendingSkill = skill;
    }

    public void CancelTargeting()
    {
        _pendingSkill = null;
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
                ConfirmEnemy(worldPoint);
                break;
        }
    }

    private void ConfirmGroundPoint(Vector3 worldPoint)
    {
        Skill skill = _pendingSkill;
        CancelTargeting();
        skill.Activate(new SkillCastContext(worldPoint, null, CasterObject));
    }

    private void ConfirmEnemy(Vector3 worldPoint)
    {
        BaseMonster target = FindEnemyUnderPointer(worldPoint, _pendingSkill.TargetLayers);

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
        screenPos.z = 0f;
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
