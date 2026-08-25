using UnityEngine;

// 연구·용 스킬트리의 타워 최대체력 보너스를 실제 타워에 반영한다.
// 두 트리의 기여는 TowerMaxHealthMultiplierComposite가 합성하므로 이쪽은 그 하나만 읽는다.
//
// 공격력·공속·사거리는 TowerAttack이 매 공격마다 ITowerStatMultiplierQuery로 pull하므로
// 속성을 바꾸는 즉시 반영되지만, 최대체력만은 Health가 최대치를 "값"으로 들고 있어
// Tower.Setup 시점에 한 번 확정된다. 그래서 확정 시점을 규칙으로 못박는다 -
// 밤 시작 시점에 그날의 최대체력이 결정되고, 밤 도중에는 변하지 않는다
// (속성 변경은 낮에만 가능하므로 플레이어가 손해를 보는 타이밍이 생기지 않는다).
//
// OnNightStart를 쓰는 이유: OnDayReady에 걸면 낮 동안 속성을 바꿀 때마다 전 타워의
// 체력 상한이 출렁여, 수리·피해 계산을 플레이어가 예측할 수 없게 된다.
public sealed class TowerMaxHealthApplier : MonoBehaviour
{
    [SerializeField] private TowerMaxHealthMultiplierComposite _maxHealthComposite;
    [SerializeField] private CycleManager _cycleManager;
    [SerializeField] private GridMap _gridMap;

    [Tooltip("새 게임 +(뮤테이터) 서비스. 비워 두면 뮤테이터 없는 표준 모드로 동작한다"
        + " - 튜토리얼·테스트 씬에는 두지 않는다.")]
    [SerializeField]
    [WiringOptional]
    private RunModifierService _runModifierService;

    private void OnEnable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.AddListener(HandleNightStart);
        }
    }

    private void OnDisable()
    {
        if (_cycleManager != null)
        {
            _cycleManager.OnNightStart.RemoveListener(HandleNightStart);
        }
    }

    private void HandleNightStart(int cycle)
    {
        ApplyAll();
    }

    private void ApplyAll()
    {
        if (!WiringGuard.Require(_maxHealthComposite, nameof(_maxHealthComposite), this) ||
            !WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        // 약한 지반(weak_ground)의 런 배율. 타워 종류와 무관한 런 전역 값이라 루프 밖에서 한 번만 읽는다.
        // 새 곱셈 지점을 만들지 않고 기존 배율 옆에 곱하는 이유: Tower.ApplyMaxHealthMultiplier가
        // 기저값(TowerData.MaxHealth)에 곱하고 현재/최대 비율을 보존하므로,
        // "밤 시작에 그날의 최대체력이 확정된다"는 위 규칙이 그대로 유지된다.
        float runMultiplier = RunModifiers
            .SnapshotOf(_runModifierService)
            .GetMultiplier(RunModifierChannel.TowerMaxHealth);

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is not Tower tower || tower.Data == null)
            {
                continue;
            }

            tower.ApplyMaxHealthMultiplier(
                _maxHealthComposite.GetMaxHealthMultiplier(tower.Data) * runMultiplier);
        }
    }
}
