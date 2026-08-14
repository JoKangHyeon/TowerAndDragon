using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 스킬 발동 흐름을 담당한다: 즉시 발동 / 지점 지정(광역) / 적 지정(단일).
/// 건설·점령 모드(BuildingPlacementController, ConquestModeController)와 동일한 입력 패턴
/// (전용 액션만 Enable/Disable, 공유 액션은 GlobalInputBootstrap에 맡김, 좌클릭 확정,
/// 포인터-오버-UI 가드)을 따른다.
/// </summary>
public class SkillTargetingController : MonoBehaviour, IExclusiveMode
{
    private const float ENEMY_PICK_RADIUS = 0.3f;
    // 스프라이트가 놓인 월드 Z 평면 - GetMouseWorldPoint가 이 평면 위의 지점을 구하는 데 사용한다.
    private const float TARGET_PLANE_WORLD_Z = 0f;

    [Tooltip("타겟 지정을 확정하는 액션 - 보통 좌클릭.")]
    [SerializeField] private InputActionReference _confirmAction;
    [Tooltip("타겟팅 모드를 취소하는 액션 - 보통 우클릭/ESC.")]
    [SerializeField] private InputActionReference _cancelAction;
    [Tooltip("스킬 발동자로 기록할 오브젝트. 비워두면 이 컨트롤러 자신.")]
    [WiringOptional]
    [SerializeField] private GameObject _caster;
    [Tooltip("GroundPoint 스킬 시전 중 커서를 따라다니며 실제 피해 범위를 보여줄 원형 인디케이터.")]
    [SerializeField] private RangeIndicator _rangeIndicator;
    [Tooltip("Enemy 스킬 시전 중 커서 아래 유효한 적 위에 띄울 작은 삼각형 인디케이터.")]
    [SerializeField] private SkillTargetIndicator _targetIndicator;
    [Tooltip("전역형 액티브(빙결·전역 대미지)가 대상 몬스터 목록을 얻는 데 쓴다.")]
    [SerializeField] private WaveManager _waveManager;
    [Tooltip("전역형 액티브(타워 즉시 수리)가 대상 건물 목록을 얻는 데 쓴다.")]
    [SerializeField] private GridMap _gridMap;
    [Tooltip("생명 액티브(성 즉시 회복)가 회복 대상으로 쓴다.")]
    [SerializeField] private Castle _castle;
    [Tooltip("타겟팅에 들어갈 때 다른 배타 모드(건설·점령·인구 등)를 닫는 데 쓴다.")]
    [SerializeField] private UIManager _uiManager;
    [Tooltip("타겟팅 중 같은 좌클릭이 건물 배치/선택으로도 처리되지 않도록 입력을 억제할 대상.")]
    [SerializeField] private BuildingPlacementController _buildingPlacementController;

    private Camera _cam;
    private Skill _pendingSkill;
    private BaseMonster _hoveredEnemy;

    public bool IsTargeting => _pendingSkill != null;

    private GameObject CasterObject => _caster != null ? _caster : gameObject;

    private void Awake()
    {
        _cam = Camera.main;
    }

    // _cancelAction(Esc)은 모든 창이 함께 쓰는 공유 액션이라 GlobalInputBootstrap이 켠다 -
    // 여기서 끄면 이 컨트롤러가 비활성화되는 순간 모든 창의 Esc가 같이 죽는다.
    // _confirmAction(좌클릭)은 이 컨트롤러 전용이므로 여기서 직접 관리한다
    // (BuildingPlacementController의 _placeAction / _cancelMoveAction 구분과 같은 기준).
    private void OnEnable()
    {
        if (_confirmAction != null)
            _confirmAction.action.Enable();
    }

    private void OnDisable()
    {
        if (_confirmAction != null)
            _confirmAction.action.Disable();
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
            skill.Activate(new SkillCastContext(
                Vector3.zero,
                null,
                CasterObject,
                _waveManager != null ? _waveManager.SpawnedMonsters : null,
                _gridMap != null ? _gridMap.Buildings : null,
                _castle));
            return;
        }

        // 다른 스킬을 타겟팅하던 중 스킬을 바꿔 누르는 경우, 이전 인디케이터가 남아있지 않도록 먼저 정리한다.
        CancelTargeting();
        _pendingSkill = skill;

        // CloseAllExcept를 먼저 부른다 - 점령/인구 모드가 닫히면서 InputSuppressed를 false로
        // 되돌리므로, 그 뒤에 true로 세워야 한다. (InputSuppressed는 참조 카운트가 없는 단일
        // bool이라 쓰는 순서가 곧 정확성이다.)
        if (_uiManager != null)
        {
            _uiManager.CloseAllExcept(this);
        }

        if (_buildingPlacementController != null)
        {
            _buildingPlacementController.InputSuppressed = true;
        }

        if (skill.Targeting == SkillTargeting.GroundPoint && _rangeIndicator != null)
        {
            _rangeIndicator.Show(skill.AreaRadius, skill.AreaRadius * IsometricMath.RADIUS_Y_RATIO);
        }
    }

    public void CancelTargeting()
    {
        // 타겟팅 중이 아니었다면 InputSuppressed는 점령/인구 모드 등 다른 주인의 것이므로 건드리지 않는다.
        bool wasTargeting = IsTargeting;

        if (_pendingSkill != null)
        {
            _pendingSkill.ClearPreview();
        }

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

        if (wasTargeting && _buildingPlacementController != null)
        {
            _buildingPlacementController.InputSuppressed = false;
        }
    }

    // 타겟팅 중 GroundPoint 스킬이면 인디케이터를 커서 위치로 갱신한다.
    private void UpdateRangeIndicator()
    {
        if (_rangeIndicator == null || _pendingSkill.Targeting != SkillTargeting.GroundPoint)
            return;

        Vector3 mousePos = GetMouseWorldPoint();
        Vector3 centerPos = _pendingSkill.GetTargetCenter(mousePos);
        
        _rangeIndicator.SetCenter(centerPos);
        _pendingSkill.UpdatePreview(mousePos);
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
        if (_cancelAction != null && _cancelAction.action.WasPressedThisFrame())
        {
            CancelTargeting();
        }
    }

    // WasPerformedThisFrame이 아니라 WasPressedThisFrame을 쓴다 - 이 액션(UI/Click)은 PassThrough라
    // performed가 누를 때와 뗄 때 두 번 성립해 한 번의 클릭으로 스킬이 두 번 발동한다.
    private void HandleConfirmInput()
    {
        if (_confirmAction == null || !_confirmAction.action.WasPressedThisFrame())
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
        Vector3 snappedPoint = skill.GetTargetCenter(worldPoint);
        CancelTargeting();
        skill.Activate(new SkillCastContext(snappedPoint, null, CasterObject));
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

    bool IExclusiveMode.IsOpen => IsTargeting;

    // 타겟팅 진입은 "어떤 스킬인가"라는 인자가 필요해 인자 없는 Open()으로 표현할 수 없다.
    // 이 모드는 "다른 모드가 열리면 닫힌다"는 한 방향으로만 레지스트리에 참여하고,
    // 반대 방향(진입 시 남을 닫기)은 BeginTargeting이 CloseAllExcept로 직접 처리한다.
    void IExclusiveMode.Open() { }

    void IExclusiveMode.Close() => CancelTargeting();
}
