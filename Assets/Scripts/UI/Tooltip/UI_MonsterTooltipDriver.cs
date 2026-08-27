using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 월드에 있는 적에 커서를 올리면 툴팁을 띄운다(누르지 않아도 된다).
/// 이름·설명 같은 종류 정보에 더해 현재 체력과 지금 걸린 버프/디버프를 함께 보여준다.
///
/// UI_TooltipTrigger를 쓰지 못하는 이유는 UI_BuildingTooltipDriver와 같다: Main Camera에
/// Physics2DRaycaster가 없어 월드 오브젝트에는 포인터 이벤트가 오지 않고, 그렇다고 Raycaster를
/// 붙이면 EventSystem.IsPointerOverGameObject()가 월드 콜라이더까지 "UI 위"로 판정해
/// 건물 배치·선택·카메라 드래그·스킬 타겟팅이 한꺼번에 막힌다. 그래서 폴링으로 커서 아래를 직접 본다.
///
/// 표시기는 건물·자원 툴팁과 공유해도 된다 - UI_TooltipPresenter가 owner 토큰으로 소유권을
/// 가리므로 서로의 툴팁을 지우지 않는다.
/// </summary>
public class UI_MonsterTooltipDriver : MonoBehaviour
{
    private const float DEFAULT_HOVER_DELAY = 0.3f;

    // 상태이상의 남은 시간이 계속 줄어들므로 갱신 주기가 실제로 일을 한다(건물 툴팁과 같은 값).
    private const float DEFAULT_REFRESH_INTERVAL = 0.25f;

    // 적 레이어는 씬마다 흩어진 인스펙터 값 대신 여기서 한 번만 계산한다 - Skill.cs의
    // MeteorBarricadeSkill과 같은 선례. WiringChecker는 LayerMask가 Nothing으로 남아도
    // "값이 있다"고 보므로 씬 간 값 어긋남을 도구로 잡을 수 없다.
    private static readonly int ENEMY_LAYER_MASK = LayerMask.GetMask(Defines.ENEMY_LAYER_NAME);

    [SerializeField] private UI_TooltipPresenter _presenter;

    [Tooltip("스킬 타겟팅 중에는 툴팁을 띄우지 않는다(타겟 인디케이터와 겹친다). 비우면 그 판정을 건너뛴다.")]
    [SerializeField] private SkillTargetingController _skillTargetingController;

    [Tooltip("커서를 올린 뒤 툴팁이 뜨기까지 머물러야 하는 시간(초). 0이면 스쳐 지나가도 떠서 깜빡인다.")]
    [SerializeField] private float _hoverDelay = DEFAULT_HOVER_DELAY;

    [Tooltip("표시 중인 툴팁의 값을 다시 계산하는 주기(초). 체력과 상태 남은 시간이 계속 바뀐다.")]
    [SerializeField] private float _refreshInterval = DEFAULT_REFRESH_INTERVAL;

    // 상태 줄을 매번 새로 할당하지 않기 위해 재사용한다 - 0.25초마다 다시 만드는 목록이다.
    private readonly List<MonsterStatusLine> _statusLines = new();

    private Camera _camera;

    // 커서가 지금 얹혀 있는 적. 아직 지연 시간을 못 채웠으면 표시되지 않은 상태다.
    private BaseMonster _hoveredMonster;

    // 실제로 툴팁을 띄운 적. 내용을 다시 만들 때 쓴다.
    private BaseMonster _shownMonster;

    // 표시 중인지는 개체 참조가 아니라 이 플래그로 판정한다 - 띄워 둔 적이 죽으면 Unity의
    // == 오버로드 때문에 _shownMonster != null이 false가 되어, 닫아야 할 순간에 Hide가
    // 조기 반환하고 툴팁이 옛 내용 그대로 화면에 남는다. 적은 죽어서 사라지는 것이 정상 동작이라
    // 건물보다 훨씬 자주 걸린다.
    private bool _isShowing;

    private float _hoverElapsed;
    private float _refreshElapsed;

    private bool IsShowing => _isShowing;

    private void OnEnable()
    {
        StringTable.OnLanguageChanged += HandleLanguageChanged;
    }

    // 호버 상태까지 함께 지운다 - Hide만 부르면 커서를 적에 올려 둔 채 비활성→재활성했을 때
    // 지연을 이미 채운 것으로 보고 툴팁이 즉시 뜬다.
    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= HandleLanguageChanged;
        ClearHover();
    }

    private void Update()
    {
        if (!TryResolveTarget(out BaseMonster monster))
        {
            ClearHover();
            return;
        }

        if (!ReferenceEquals(monster, _hoveredMonster))
        {
            // 다른 적으로 옮겨갔다. 이전 툴팁을 즉시 걷고 지연을 처음부터 다시 센다.
            Hide();
            _hoveredMonster = monster;
            _hoverElapsed = 0f;
        }

        // 호버 판정은 게임 속도와 무관한 UI 피드백이라 unscaled로 센다(건물 툴팁과 같은 이유).
        _hoverElapsed += Time.unscaledDeltaTime;

        if (_hoverElapsed < _hoverDelay)
        {
            return;
        }

        if (IsShowing)
        {
            FollowCursor();
            RefreshPeriodically();
            return;
        }

        Show(monster);
    }

    // 툴팁을 띄우면 안 되는 상황을 먼저 걸러낸다.
    private bool TryResolveTarget(out BaseMonster monster)
    {
        monster = null;

        if (IsPointerOverUI || IsTargetingSkill)
        {
            return false;
        }

        if (_camera == null)
        {
            _camera = Camera.main;
        }

        if (_camera == null)
        {
            return false;
        }

        monster = MonsterPicker.FindUnderPointer(
            MonsterPicker.GetMouseWorldPoint(_camera),
            ENEMY_LAYER_MASK);

        return monster != null;
    }

    private void Show(BaseMonster monster)
    {
        if (!WiringGuard.Require(_presenter, nameof(_presenter), this))
        {
            return;
        }

        if (!TryBuildContent(monster, out TooltipContent content))
        {
            return;
        }

        _shownMonster = monster;
        _isShowing = true;
        _refreshElapsed = 0f;
        _presenter.Show(this, content, PointerScreenPosition);
    }

    private void RefreshPeriodically()
    {
        _refreshElapsed += Time.unscaledDeltaTime;

        if (_refreshElapsed < _refreshInterval)
        {
            return;
        }

        _refreshElapsed = 0f;
        RefreshNow();
    }

    // 띄워 둔 적이 죽었거나 사라졌으면 닫는다.
    private void RefreshNow()
    {
        if (TryBuildContent(_shownMonster, out TooltipContent content))
        {
            _presenter.Refresh(this, content);
            return;
        }

        Hide();
    }

    private bool TryBuildContent(BaseMonster monster, out TooltipContent content)
    {
        content = default;

        // 죽은 적의 툴팁은 남기지 않는다 - 사망 연출 동안 참조는 살아 있어도 보여줄 상태가 아니다.
        if (monster == null || monster.IsDead)
        {
            return false;
        }

        _statusLines.Clear();
        monster.CollectActiveStatuses(_statusLines);
        EnemyEnhancementStatusLines.Collect(monster.Enhancement, _statusLines);

        content = MonsterTooltipBuilder.Build(MonsterTooltipInput.ForInstance(monster, _statusLines));

        return content.HasContent;
    }

    // 언어가 바뀌면 즉시 다시 만든다 - 다음 갱신 주기까지 기다리면 옛 언어가 그대로 보인다.
    private void HandleLanguageChanged()
    {
        if (IsShowing)
        {
            RefreshNow();
        }
    }

    private void FollowCursor()
    {
        _presenter.MoveTo(PointerScreenPosition);
    }

    private void ClearHover()
    {
        Hide();
        _hoveredMonster = null;
        _hoverElapsed = 0f;
    }

    // 남의 툴팁은 건드리지 않는다 - 표시기가 owner를 대조해 내가 띄운 것만 닫는다.
    private void Hide()
    {
        if (!IsShowing)
        {
            return;
        }

        _isShowing = false;
        _shownMonster = null;
        _presenter?.Hide(this);
    }

    private static Vector2 PointerScreenPosition =>
        Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    private static bool IsPointerOverUI =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private bool IsTargetingSkill =>
        _skillTargetingController != null && _skillTargetingController.IsTargeting;
}
