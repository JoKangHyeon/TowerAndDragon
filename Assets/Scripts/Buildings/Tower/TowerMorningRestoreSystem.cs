using UnityEngine;

/// <summary>
/// 아침(낮 시작)마다 모든 타워를 만피로 회복시키고, 부활 대기 중인 타워를 즉시 재활성화한다.
/// 복구 자체는 이미 있는 Tower.RestoreAtMorning()이 담당한다 - 새 복구 경로를 만들지 않는다.
/// CastleRegenSystem과 같은 독립 시스템 컴포넌트 스타일을 따른다 -
/// Tower에 CycleManager 참조를 새로 넣지 않는다.
///
/// <b>이 컴포넌트가 no_morning_restore(긴 밤) 뮤테이터의 두 절반을 모두 소유한다.</b>
/// (a) 아침 복구를 건너뛰고, (b) <see cref="ITowerReviveGateQuery"/>로 낮 동안 부활 게이지를 멈춘다.
/// 둘을 갈라 두면 한쪽만 켜졌을 때 조용히 무효가 된다 - 아침 복구만 막으면 낮에도 게이지가 차올라
/// ReviveDelay초 뒤 알아서 부활하고, 낮은 시간 제한이 없으므로 사실상 100% 부활한다.
/// 그래서 낮/밤을 아는 이 오브젝트가 관문 구현까지 함께 맡는다.
/// </summary>
public sealed class TowerMorningRestoreSystem : MonoBehaviour, ITowerReviveGateQuery
{
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private GridMap _gridMap;

    [Tooltip("새 게임 +(뮤테이터) 서비스. 비워 두면 뮤테이터 없는 표준 모드로 동작한다"
        + " - 튜토리얼·테스트 씬에는 두지 않는다.")]
    [SerializeField]
    [WiringOptional]
    private RunModifierService _runModifierService;

    // 긴 밤(no_morning_restore)이 켜져 있는가. 서비스가 없으면 항상 false라 기존 동작과 같다.
    private bool IsNoMorningRestore =>
        RunModifiers.SnapshotOf(_runModifierService).HasRule(RunRuleFlag.NoMorningRestore);

    /// <summary>낮에는 부활 게이지를 멈춘다 - 단, 뮤테이터가 꺼져 있으면 언제나 진행한다.
    /// 주기를 알 수 없으면 진행시킨다(프로젝트 공통의 fail-open 규약).</summary>
    bool ITowerReviveGateQuery.CanAdvanceRevive =>
        !IsNoMorningRestore ||
        _cycleManager == null ||
        _cycleManager.CurrentCycle == CycleManager.CycleState.Night;

    private void OnEnable()
    {
        if (WiringGuard.Require(_cycleManager, nameof(_cycleManager), this))
        {
            _cycleManager.OnDayStart.AddListener(RestoreAll);
        }

        // 런 도중에 지어진 타워에도 관문을 물려야 한다 - 이미 있는 타워는 Start에서 한 번 순회한다.
        // ResearchManager가 전투 수리 해금 쿼리를 배포하는 방식과 같다.
        if (WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            _gridMap.OnBuildingAdded.AddListener(AssignReviveGate);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnDayStart.RemoveListener(RestoreAll);
        }

        if (_gridMap == null)
        {
            return;
        }

        _gridMap.OnBuildingAdded.RemoveListener(AssignReviveGate);

        // 뗄 때는 관문을 null로 되돌린다 - 남겨 두면 파괴된 시스템을 계속 물어보게 된다
        // (TowerStatMultiplierCoordinator가 오라 시스템을 뗄 때와 같은 해제 규약).
        foreach (Building building in _gridMap.Buildings)
        {
            if (building is Tower tower)
            {
                tower.SetReviveGateQuery(null);
            }
        }
    }

    // 씬에 이미 놓여 있던 타워는 OnBuildingAdded가 발화하지 않으므로 여기서 한 번 훑는다.
    // Start인 이유: GridMap.Awake가 그리드를 다 세운 뒤여야 Buildings가 채워져 있다.
    private void Start()
    {
        if (_gridMap == null)
        {
            return;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            AssignReviveGate(building);
        }
    }

    private void AssignReviveGate(Building building)
    {
        if (building is Tower tower)
        {
            tower.SetReviveGateQuery(this);
        }
    }

    // OnDayStart의 일차 인자는 쓰지 않는다 - 복구량은 날짜와 무관하다.
    private void RestoreAll(int _)
    {
        // 긴 밤: 아침 복구를 건너뛴다. 부서진 타워는 낮에도 부서진 채로 남는다.
        // 게이지 정지는 CanAdvanceRevive가 맡는다 - 여기서만 막으면 뮤테이터가 무효가 된다.
        if (IsNoMorningRestore)
        {
            return;
        }

        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is Tower tower)
            {
                tower.RestoreAtMorning();
            }
        }
    }
}
