using System.Collections.Generic;
using UnityEngine;

public sealed class TowerAuraSystem : MonoBehaviour
{
    [SerializeField] private GridMap _gridMap;

    // 오라 보정은 프레임마다 한 번, 모든 타워에 대해 한꺼번에 계산해 표로 들고 있는다.
    // ResolveModifiers는 그 표를 읽을 뿐이다.
    //
    // 왜 이 구조인가 - TowerPopulation.CanOperate/StaffingRatio는 오라로 받은 보너스 인구
    // (SoulPopulation)를 포함하므로, "오라의 활성 판정"과 "오라의 결과"가 서로를 참조한다.
    // 예전 구현은 ResolveModifiers 안에서 다른 타워의 CanOperate를 그대로 읽어 상호 재귀에 빠졌고,
    // 재진입 가드로 크래시만 막았다. 가드는 같은 타워로 되돌아오는 것만 끊고 다른 타워로 내려가는
    // 분기는 끊지 못하므로, 한 번의 호출이 오라 타워 k개의 순열 수(약 e·k!)만큼 중첩 호출로 퍼졌다.
    // 오라 타워 5개·타워 10개 배치에서 타워마다 CanOperate를 한 번 읽는 데 프레임당 162ms가 나왔다.
    // (예전 주석이 "눈에 보이는 문제가 확인되면 고정점 반복으로 다시 봐야 한다"고 남겨둔 지점이다.)
    //
    // 그래서 순환을 고정점 반복으로 푼다. 오라의 세기는 CombineStrongest(전부 Max/OR)로 합쳐지고
    // SoulPopulation은 충원율을, 충원율은 반경을 단조 증가시키므로, Neutral에서 출발한 반복은
    // 유일한 최소 고정점으로 수렴한다 - 순회 순서가 결과를 바꾸지 않는다. 예전 주석이 근사라고
    // 적어둔 "영혼 타워끼리 서로의 범위에 들어온 배치"도 이 구조에서는 정확해진다.
    // 바닥이 Neutral이라 실제 인구가 0인 영혼 타워끼리 서로를 가동시키는 일도 생기지 않는다.
    //
    // 재귀가 구조적으로 불가능한 이유: EnsureConverged는 계산을 시작하기 전에 프레임 번호를
    // 먼저 찍는다. 계산 중 CanOperate가 ResolveModifiers를 되짚어 들어와도 표를 읽고 즉시 돌아간다.
    // 이 순서를 바꾸면 재귀가 되살아난다.
    private readonly List<Tower> _towers = new();
    private readonly List<AuraSource> _auraSources = new();
    private readonly Dictionary<Tower, TowerAuraModifiers> _modifiersByTower = new();
    private int _convergedFrame = -1;

    // 오라를 뿜는 쪽 한 개. Staffing을 들고 있는 이유는 프레임마다 한 번만 GetComponent 하려는
    // 것이다 - 예전에는 target마다, 그리고 중첩 호출마다 인터페이스 GetComponent를 다시 했다.
    private struct AuraSource
    {
        public Tower Tower;
        public TowerAuraDataSO Aura;
        public ITowerStaffing Staffing;
        public float EffectiveRadius;
    }

    public TowerAuraModifiers ResolveModifiers(Tower target)
    {
        if (_gridMap == null || target == null)
        {
            return TowerAuraModifiers.Neutral;
        }

        EnsureConverged();

        // 그리드에 아직 등록되지 않은 타워는 표에 없다 - 수렴이 끝난 소스 상태로 즉석 계산한다.
        // 이 경로도 재귀하지 않는다(소스의 CanOperate가 표를 읽고 돌아오므로).
        return _modifiersByTower.TryGetValue(target, out TowerAuraModifiers modifiers)
            ? modifiers
            : Combine(target);
    }

    private void EnsureConverged()
    {
        if (Time.frameCount == _convergedFrame)
        {
            return;
        }

        // 계산보다 먼저 찍는다 - 위에 적은 재귀 차단 불변식.
        _convergedFrame = Time.frameCount;

        CollectTowers();
        ResetTable();

        // 소스가 k개면 한 번에 최소 하나씩은 확정되므로 k + 1회 안에 반경이 멈춘다
        // (마지막 1회는 변화 없음 확인). ScaleRadiusWithStaffing 때문에 반경이 연속값이라
        // 이 상한을 불변식으로 믿지는 않고, 소진되어 빠져나온 경우에도 표를 한 번 더 맞춘다.
        int maxPasses = _auraSources.Count + 1;
        bool hasChanged = false;

        for (int pass = 0; pass < maxPasses; pass++)
        {
            hasChanged = RefreshSourceRadii();

            if (!hasChanged)
            {
                return;
            }

            RebuildTable();
        }

        if (hasChanged)
        {
            Debug.LogWarning(
                $"[TowerAuraSystem] 오라 반경이 {maxPasses}회 반복 안에 수렴하지 않았습니다. " +
                $"소스 {_auraSources.Count}개 - 마지막 값으로 확정합니다.",
                this);

            RebuildTable();
        }
    }

    private void CollectTowers()
    {
        _towers.Clear();
        _auraSources.Clear();

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is not Tower tower)
            {
                continue;
            }

            _towers.Add(tower);

            if (tower.Data is not ITowerAuraDataProvider provider ||
                !provider.HasTowerAura)
            {
                continue;
            }

            _auraSources.Add(new AuraSource
            {
                Tower = tower,
                Aura = provider.TowerAura,
                Staffing = tower.GetComponent<ITowerStaffing>(),
                EffectiveRadius = 0f,
            });
        }
    }

    private void ResetTable()
    {
        _modifiersByTower.Clear();

        for (int i = 0; i < _towers.Count; i++)
        {
            _modifiersByTower[_towers[i]] = TowerAuraModifiers.Neutral;
        }
    }

    // 이번 반복의 소스 반경을 모두 다시 구한다. 표는 아직 건드리지 않으므로 이 안의 모든 소스가
    // 같은(직전 반복의) 표를 본다 - 반복 한 회 안에서 순회 순서가 결과에 끼어들지 않게 하려는 것.
    private bool RefreshSourceRadii()
    {
        bool hasChanged = false;

        for (int i = 0; i < _auraSources.Count; i++)
        {
            AuraSource source = _auraSources[i];

            float radius = TryGetActiveAura(
                source.Tower,
                source.Staffing,
                out _,
                out float effectiveRadius)
                ? effectiveRadius
                : 0f;

            if (!Mathf.Approximately(radius, source.EffectiveRadius))
            {
                hasChanged = true;
            }

            source.EffectiveRadius = radius;
            _auraSources[i] = source;
        }

        return hasChanged;
    }

    private void RebuildTable()
    {
        for (int i = 0; i < _towers.Count; i++)
        {
            Tower tower = _towers[i];

            // 목록은 프레임 동안 고정된 스냅샷이다 - 예전 구현처럼 호출마다 Buildings를 다시 읽지
            // 않으므로, 프레임 중간에 철거·이동으로 사라진 타워가 목록에 남아있을 수 있다.
            // 여기서 걸러야 아래 Combine이 파괴된 Transform을 역참조하지 않는다.
            if (tower == null)
            {
                continue;
            }

            _modifiersByTower[tower] = Combine(tower);
        }
    }

    private TowerAuraModifiers Combine(Tower target)
    {
        TowerAuraModifiers modifiers = TowerAuraModifiers.Neutral;

        for (int i = 0; i < _auraSources.Count; i++)
        {
            AuraSource source = _auraSources[i];

            // 소스도 같은 이유로 프레임 중간에 사라질 수 있다(RebuildTable 주석 참고).
            if (source.Tower == null ||
                source.Tower == target ||
                source.EffectiveRadius <= 0f ||
                !IsWithinAura(source.Tower, target, source.EffectiveRadius))
            {
                continue;
            }

            modifiers = modifiers.CombineStrongest(source.Aura);
        }

        return modifiers;
    }

    // 배치 미리보기·선택 표시처럼 표 밖에서 한 번씩 묻는 곳을 위한 입구. 여기서 하는
    // GetComponent는 프레임마다 도는 경로가 아니다(오라 계산은 위의 캐시된 Staffing을 쓴다).
    public static bool TryGetActiveAura(
        Tower source,
        out TowerAuraDataSO aura,
        out float effectiveRadius)
    {
        return TryGetActiveAura(
            source,
            source != null ? source.GetComponent<ITowerStaffing>() : null,
            out aura,
            out effectiveRadius);
    }

    private static bool TryGetActiveAura(
        Tower source,
        ITowerStaffing staffing,
        out TowerAuraDataSO aura,
        out float effectiveRadius)
    {
        aura = null;
        effectiveRadius = 0f;

        if (source == null ||
            source.IsReviving ||
            source.IsParalyzed ||
            source.Data is not ITowerAuraDataProvider provider ||
            !provider.HasTowerAura)
        {
            return false;
        }

        aura = provider.TowerAura;
        float radiusMultiplier = 1f;

        if (source is BabyDragonTower babyDragon)
        {
            if (!babyDragon.CanOperate ||
                babyDragon.Mode != BabyDragonMode.Buff)
            {
                return false;
            }
        }
        else
        {
            // SoulPopulation(오라로 받은 보너스 인구)까지 포함한 실제 값을 쓴다 - 은신/시간 타워가
            // 영혼 타워로 정원을 채웠을 때도 오라 반경이 100%까지 정상적으로 커지게 하려는 것.
            // 이게 만드는 순환은 ResolveModifiers의 고정점 반복이 푼다.
            if (staffing == null || !staffing.CanOperate)
            {
                return false;
            }

            if (aura.ScaleRadiusWithStaffing)
            {
                radiusMultiplier =
                    Mathf.Clamp01(staffing.StaffingRatio);
            }
        }

        effectiveRadius = aura.Radius * radiusMultiplier;
        return effectiveRadius > 0f;
    }

    private static bool IsWithinAura(
        Tower source,
        Tower target,
        float radius)
    {
        float radiusY =
            radius * IsometricMath.RADIUS_Y_RATIO;

        return IsometricMath.IsWithinEllipse(
            target.transform.position,
            source.transform.position,
            radius,
            radiusY);
    }

    public static bool TryGetPreviewRadius(
        TowerData towerData,
        out float radius)
    {
        radius = 0f;

        if (towerData is not ITowerAuraDataProvider provider ||
            !provider.HasTowerAura)
        {
            return false;
        }

        radius = provider.TowerAura.Radius;
        return radius > 0f;
    }
}
