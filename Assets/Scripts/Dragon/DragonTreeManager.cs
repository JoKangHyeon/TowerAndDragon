using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 용 스킬트리 매니저. ResearchManager와 대칭 구조이되 공용 판정(선행·게이트·자원)은
// ProgressionManagerBase에 위임한다.
// 이름을 로드맵 문서의 DragonSkillManager 대신 DragonTreeManager로 둔 이유:
// 기존 전투 액티브 스킬 매니저 Assets/Scripts/UI/Skill/SkillManager.cs와의 이름 충돌 회피
// (Build3_프로토타입_일정계획.md 리스크 항목).
//
// ResearchManager와 동일한 pull-query 집계 패턴(완료 노드 → 효과 → 합산)으로
// ITowerStatMultiplierQuery/IChunkYieldMultiplierQuery/IVisionRadiusBonusQuery/
// IConquestModifierQuery/ITowerHitStatusQuery를 구현한다 - 소비 지점은 Composite를 통해
// 연구와 이 매니저 양쪽에서 값을 받는다(Assets/Scripts/Modifiers/ 참고).
public sealed class DragonTreeManager : ProgressionManagerBase,
    ITowerStatMultiplierQuery,
    IChunkYieldMultiplierQuery,
    IVisionRadiusBonusQuery,
    IConquestModifierQuery,
    ITowerHitStatusQuery
{
    private const float BASE_DAMAGE_MULTIPLIER = 1f;
    private const float BASE_RANGE_MULTIPLIER = 1f;
    private const float BASE_ATTACK_SPEED_MULTIPLIER = 1f;
    private const float BASE_YIELD_MULTIPLIER = 1f;
    private const float BASE_MAX_HEALTH_MULTIPLIER = 1f;
    private const float BASE_BARRICADE_HEALTH_MULTIPLIER = 1f;

    // 랭크 비교의 초기값 - 실제 노드 랭크는 1부터라 어떤 노드든 이 값보다 크다.
    private const int NO_RANK = 0;

    [SerializeField] private DragonSkillTreeData _tree;
    [SerializeField] private UnityEvent<DragonType> _activeAttributeChanged = new();

    // 안내가 어미용 진행을 막는 동안 등록된다. 비어 있는 것이 평소 상태라 본게임은 이 관문을
    // 지나지 않는다 - 튜토리얼 씬에만 있는 컴포넌트가 등록하기 때문이다.
    private readonly List<IDragonProgressionGateQuery> _gateQueries = new();

    protected override ProgressionTreeData TreeData => _tree;

    /// <summary>어미용 진행 관문을 건다. 같은 대상을 두 번 넣어도 한 번만 등록된다.</summary>
    public void AddGateQuery(IDragonProgressionGateQuery query)
    {
        if (query != null && !_gateQueries.Contains(query))
        {
            _gateQueries.Add(query);
        }
    }

    /// <summary>등록을 뗀다. 자기가 넣은 것만 빼므로 남의 관문은 건드리지 않는다.</summary>
    public void RemoveGateQuery(IDragonProgressionGateQuery query)
    {
        _gateQueries.Remove(query);
    }

    /// <summary>지금 스킬 노드를 새로 열 수 있는지. 표시(회색 처리)와 실제 해금이 같은 판정을 쓴다.</summary>
    public bool CanUnlockSkillNow => !IsGateClosed(isSkillUnlock: true);

    /// <summary>지금 어미용 속성을 바꿀 수 있는지. 속성 변경은 이 매니저를 지나지 않으므로 호출부가 직접 묻는다.</summary>
    public bool CanChangeAttributeNow => !IsGateClosed(isSkillUnlock: false);

    /// <summary>
    /// 막힌 시도가 실제로 거절된 순간, 막은 쪽에 사유를 알리게 한다.
    /// 판정(<see cref="CanUnlockSkillNow"/> 등)은 UI가 매 프레임 물어보므로 그 자리에서 알리면
    /// 같은 문구가 프레임마다 다시 뜬다 - 알림은 시도 경로에서만 부른다.
    /// </summary>
    public void NotifyProgressionBlocked()
    {
        foreach (IDragonProgressionGateQuery query in _gateQueries)
        {
            query?.NotifyDragonProgressionBlocked();
        }
    }

    /// <summary>
    /// 안내가 막고 있으면 노드를 열 수 없는 상태로 만든다. 게이트와 자원 사이에서 판정하므로
    /// 밤·선행 조건 같은 더 근본적인 사유가 먼저 표시되고, 자원 부족보다는 먼저 걸린다.
    ///
    /// 여기에 두는 이유는 <see cref="ProgressionManagerBase.GetNodeState"/>가 TryUnlock의 유일한
    /// 판정 경로여서다 - UI가 어느 버튼으로 부르든, 디버그 도구로 부르든 같이 막힌다.
    /// </summary>
    protected override ProgressionNodeState CheckExtraCost(ProgressionNodeData node) =>
        CanUnlockSkillNow ? ProgressionNodeState.Available : ProgressionNodeState.TutorialLocked;

    // 막는 쪽이 하나라도 있으면 막되, 예외를 말하는 쪽이 하나라도 있으면 통과시킨다
    // (UIManager.CanUseShortcut과 같은 형태).
    private bool IsGateClosed(bool isSkillUnlock)
    {
        bool isBlocked = false;
        foreach (IDragonProgressionGateQuery query in _gateQueries)
        {
            if (query != null && query.BlocksDragonProgression())
            {
                isBlocked = true;
                break;
            }
        }

        if (!isBlocked)
        {
            return false;
        }

        foreach (IDragonProgressionGateQuery query in _gateQueries)
        {
            if (query == null)
            {
                continue;
            }

            bool allows = isSkillUnlock
                ? query.AllowsDragonSkillUnlock()
                : query.AllowsDragonAttributeChange();

            if (allows)
            {
                return false;
            }
        }

        return true;
    }

    public DragonSkillTreeData Tree => _tree;
    public UnityEvent<DragonType> ActiveAttributeChanged => _activeAttributeChanged;

    // RunData.CurrentDragon이 런타임에 null일 수 있으므로(실측 - Dragon에 [Serializable] 없음)
    // 캐시하지 않고 매 조회마다 다시 확인한다. C-1/C-2가 늦어져도 창은 열리고
    // 노드는 잠금 상태로 그려진다.
    public DragonType? ActiveAttribute
    {
        get
        {
            Dragon dragon = Game != null ? Game.CurrentRun?.CurrentDragon : null;
            return dragon != null ? dragon.CurrentType : (DragonType?)null;
        }
    }

    public int UnlockedKinCount
    {
        get
        {
            int count = 0;

            foreach (string nodeId in UnlockedIds)
            {
                if (!TryGetNode(nodeId, out ProgressionNodeData node) ||
                    !(node is DragonSkillNodeData dragonNode))
                {
                    continue;
                }

                if (dragonNode.Kind == DragonNodeKind.KinTower || dragonNode.Kind == DragonNodeKind.KinArea)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool HasBabyDragon(DragonType attribute)
    {
        List<BabyDragon> babyDragons = Game != null ? Game.CurrentRun?.BabyDragons : null;

        if (babyDragons == null)
        {
            return false;
        }

        foreach (BabyDragon babyDragon in babyDragons)
        {
            if (babyDragon != null && babyDragon.DragonType == attribute)
            {
                return true;
            }
        }

        return false;
    }

    // 트리에 등록된 모든 스킬(해금 여부 무관) - "이 SkillSO가 용 스킬트리 소속인지" 판별하는 데 쓴다
    // (SkillManager.AvailableSkills). UnlockedActiveSkills로 판별하면 미해금 상태에서 대상이
    // 아무것도 안 걸려 "용 스킬트리 무관 스킬"로 오판, 해금 전인데도 HUD에 노출/사용 가능해진다.
    public IEnumerable<SkillSO> AllTreeSkills
    {
        get
        {
            if (Tree == null)
            {
                yield break;
            }

            foreach (DragonSkillNodeData node in Tree.DragonNodes)
            {
                if (node == null)
                {
                    continue;
                }

                foreach (DragonSkillEffectSO effect in node.Effects)
                {
                    SkillSO skill = effect != null ? effect.GetUnlockedSkill() : null;

                    if (skill != null)
                    {
                        yield return skill;
                    }
                }
            }
        }
    }

    // 액티브 스킬 해금 pull API. 해금은 활성 속성과 무관하게 영구 유지된다(로드맵 §1-3) -
    // 실제 사용 가능 여부(활성 속성 일치) 필터링은 AvailableActiveSkills가 담당한다.
    public IEnumerable<SkillSO> UnlockedActiveSkills
    {
        get
        {
            foreach (string nodeId in UnlockedIds)
            {
                if (!TryGetNode(nodeId, out ProgressionNodeData node) ||
                    !(node is DragonSkillNodeData dragonNode))
                {
                    continue;
                }

                foreach (DragonSkillEffectSO effect in dragonNode.Effects)
                {
                    SkillSO skill = effect != null ? effect.GetUnlockedSkill() : null;

                    if (skill != null)
                    {
                        yield return skill;
                    }
                }
            }
        }
    }

    // 해금된 스킬 중 활성 속성과 일치하는 것만 노출한다 - SkillManager.AvailableSkills가
    // 이 목록으로 HUD/타게팅에 실제로 사용 가능한 스킬을 필터링한다.
    public IEnumerable<SkillSO> AvailableActiveSkills
    {
        get
        {
            DragonType? active = ActiveAttribute;

            if (!active.HasValue)
            {
                yield break;
            }

            foreach (string nodeId in UnlockedIds)
            {
                if (!TryGetNode(nodeId, out ProgressionNodeData node) ||
                    !(node is DragonSkillNodeData dragonNode) ||
                    dragonNode.Attribute != active.Value)
                {
                    continue;
                }

                foreach (DragonSkillEffectSO effect in dragonNode.Effects)
                {
                    SkillSO skill = effect != null ? effect.GetUnlockedSkill() : null;

                    if (skill != null)
                    {
                        yield return skill;
                    }
                }
            }
        }
    }

    // 속성 하나가 쓰는 액티브 스킬(해금 여부 무관). 용 창의 스킬 정보 패널처럼
    // "이 속성은 이런 스킬을 쓴다"를 안내하는 곳에서 쓴다 - 실제 사용 가능 여부는
    // AvailableActiveSkills가 가른다. 속성당 액티브 노드는 하나뿐이라 첫 스킬을 돌려준다.
    public SkillSO GetActiveSkillOf(DragonType attribute)
    {
        if (Tree == null)
        {
            return null;
        }

        foreach (DragonSkillNodeData node in Tree.DragonNodes)
        {
            if (node == null || node.Attribute != attribute)
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in node.Effects)
            {
                SkillSO skill = effect != null ? effect.GetUnlockedSkill() : null;

                if (skill != null)
                {
                    return skill;
                }
            }
        }

        return null;
    }

    // 강화·궁극 노드가 부여하는 위력/쿨다운 강화분. SkillTargetingController 등 실제 발동부가
    // 조회한다 - 활성 속성과 일치하는 강화 효과만 합산된다(DragonSkillPowerEffectSO가 스스로 게이트).
    // Skill.Cooltime/DamagePercent가 매 프레임(UI_SkillIndicator) 호출하므로 Aggregate의 클로저
    // 캡처(skill)를 피하려고 직접 순회한다 - Aggregate와 같은 형태, 델리게이트 할당만 없앤 버전.
    public float GetSkillPowerMultiplierBonus(SkillSO skill)
    {
        float bonus = 0f;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    bonus += effect.GetSkillPowerMultiplierBonus(ActiveAttribute, skill);
                }
            }
        }

        return bonus;
    }

    public float GetSkillCooldownReductionRatio(SkillSO skill)
    {
        float ratio = 0f;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    ratio += effect.GetSkillCooldownReductionRatio(ActiveAttribute, skill);
                }
            }
        }

        return ratio;
    }

    // 랭크가 높은 노드의 상태이상을 우선한다 - 랭크마다 다른 상태 에셋(화상 틱뎀 강화 등)을
    // 쓰는데 첫 매치를 반환하면 UnlockedIds(HashSet)의 순회 순서에 따라 약한 쪽이 걸린다.
    public StatusEffectSO GetSpawnStatus(DragonType activeAttribute)
    {
        StatusEffectSO best = null;
        int bestRank = NO_RANK;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                StatusEffectSO status = effect != null ? effect.GetSpawnStatus(activeAttribute) : null;

                if (status != null && dragonNode.Rank > bestRank)
                {
                    best = status;
                    bestRank = dragonNode.Rank;
                }
            }
        }

        return best;
    }

    // 액티브 스킬의 일일 사용 횟수 추가분(암석 궁극). Skill.UsePerDay/Reset이 읽는다.
    public int GetSkillExtraUsePerDay(SkillSO skill)
    {
        int extra = 0;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    extra += effect.GetSkillExtraUsePerDay(ActiveAttribute, skill);
                }
            }
        }

        return extra;
    }

    // 액티브 스킬이 실제로 부여할 상태이상. 랭크가 높은 노드의 것을 우선한다 -
    // 없으면 null을 돌려주고, Skill.AppliedStatus가 SkillSO의 기본값으로 되돌아간다.
    public StatusEffectSO GetSkillStatusOverride(SkillSO skill)
    {
        StatusEffectSO best = null;
        int bestRank = NO_RANK;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                StatusEffectSO status = effect != null ? effect.GetSkillStatusOverride(ActiveAttribute, skill) : null;

                if (status != null && dragonNode.Rank > bestRank)
                {
                    best = status;
                    bestRank = dragonNode.Rank;
                }
            }
        }

        return best;
    }

    // 암석 액티브가 설치하는 방벽의 최대체력 배율. MeteorBarricadeSkill이 설치 직후 읽는다.
    public float GetBarricadeHealthMultiplier()
    {
        float bonus = 0f;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    bonus += effect.GetBarricadeHealthBonusRatio(ActiveAttribute);
                }
            }
        }

        return BASE_BARRICADE_HEALTH_MULTIPLIER + bonus;
    }

    // 새끼용 버프모드의 반경 증가분. BabyDragonBuffSystem.GetEffectiveBuffRadius가 유일한 소비자다.
    public float GetKinBuffRadiusBonusRatio(DragonType dragonType)
    {
        float bonus = 0f;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    bonus += effect.GetKinBuffRadiusBonusRatio(dragonType);
                }
            }
        }

        return bonus;
    }

    public float GetKinAreaYieldBonusRatio(DragonType dragonType)
    {
        float bonus = 0f;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    bonus += effect.GetKinAreaYieldBonusRatio(dragonType);
                }
            }
        }

        return bonus;
    }

    // --- ITowerStatMultiplierQuery ---

    public float GetDamageMultiplier(TowerData towerData) =>
        BASE_DAMAGE_MULTIPLIER + Aggregate((effect, active) => effect.GetTowerDamageMultiplierBonus(active, towerData));

    // 새끼용 타워형(KinTowerStatEffectSO)이 사거리를 다루므로 더 이상 중립값 고정이 아니다.
    public float GetRangeMultiplier(TowerData towerData) =>
        BASE_RANGE_MULTIPLIER + Aggregate((effect, active) => effect.GetTowerRangeMultiplierBonus(active, towerData));

    // 타워 최대체력 배율. 다른 스탯과 달리 매 프레임 pull되지 않는다 - Health가 최대치를 값으로
    // 들고 있어서, TowerMaxHealthApplier가 밤 시작 시점에 한 번 읽어 적용한다.
    public float GetMaxHealthMultiplier(TowerData towerData) =>
        BASE_MAX_HEALTH_MULTIPLIER + Aggregate((effect, active) => effect.GetTowerMaxHealthMultiplierBonus(active, towerData));

    public float GetAttackSpeedMultiplier(TowerData towerData) =>
        BASE_ATTACK_SPEED_MULTIPLIER + Aggregate((effect, active) => effect.GetTowerAttackSpeedMultiplierBonus(active, towerData));

    // --- IChunkYieldMultiplierQuery ---

    public float GetYieldMultiplier(Vector2Int chunkCoord, ResourceType resourceType) =>
        BASE_YIELD_MULTIPLIER + Aggregate((effect, active) => effect.GetYieldMultiplierBonus(active, chunkCoord, resourceType));

    // --- IVisionRadiusBonusQuery ---

    public int GetVisionRadiusBonus()
    {
        int bonus = 0;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    bonus += effect.GetVisionRadiusBonus(ActiveAttribute);
                }
            }
        }

        return bonus;
    }

    // --- IConquestModifierQuery ---

    public float GetConquestCostReductionRatio()
    {
        float ratio = 0f;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    ratio += effect.GetConquestCostReductionRatio(ActiveAttribute);
                }
            }
        }

        return ratio;
    }

    public int GetConquestDaysReduction()
    {
        int days = 0;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    days += effect.GetConquestDaysReduction(ActiveAttribute);
                }
            }
        }

        return days;
    }

    // --- ITowerHitStatusQuery ---

    // 새끼용 타워형(자기 속성 타워 한정, KinTowerStatusEffectSO)을 어미용 패시브(전체 타워,
    // DragonTowerHitStatusEffectSO)보다 먼저 찾는다 - 둘 다 첫 매치를 반환하는 단일 값 계약이라,
    // UnlockedIds(HashSet) 순회 순서에 따라 전역 패시브가 먼저 걸리면 새끼용 노드를 해금해도
    // 겹쳐 보이지 않는 문제가 생긴다. 대상이 좁은(BabyDragonTower 한정) 효과를 항상 우선한다.
    public StatusEffectSO GetTowerHitStatus(TowerData towerData)
    {
        StatusEffectSO kinMatch = FindTowerHitStatus(towerData, requireBabyDragonTarget: true);
        return kinMatch != null ? kinMatch : FindTowerHitStatus(towerData, requireBabyDragonTarget: false);
    }

    // GetSpawnStatus와 같은 이유로 랭크가 높은 쪽을 고른다(첫 매치 반환 금지).
    private StatusEffectSO FindTowerHitStatus(TowerData towerData, bool requireBabyDragonTarget)
    {
        DragonType? active = ActiveAttribute;
        StatusEffectSO best = null;
        int bestRank = NO_RANK;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            bool isKinNode = dragonNode.Kind == DragonNodeKind.KinTower;

            if (requireBabyDragonTarget != isKinNode)
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                StatusEffectSO status = effect != null ? effect.GetTowerHitStatus(active, towerData) : null;

                if (status != null && dragonNode.Rank > bestRank)
                {
                    best = status;
                    bestRank = dragonNode.Rank;
                }
            }
        }

        return best;
    }

    // 완료 노드 → 효과 순회를 공유하는 헬퍼. 활성 속성이 없으면(RunData 초기화 전) 0을 반환한다 -
    // 개별 효과가 매번 null 체크를 반복하지 않도록 여기서 한 번에 흡수한다.
    private float Aggregate(System.Func<DragonSkillEffectSO, DragonType?, float> selector)
    {
        float total = 0f;
        DragonType? active = ActiveAttribute;

        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                if (effect != null)
                {
                    total += selector(effect, active);
                }
            }
        }

        return total;
    }

    public void Construct(GameManager gameManager, CycleManager cycleManager, ResourceManager resourceManager)
    {
        ConstructBase(gameManager, cycleManager, resourceManager);

        if (cycleManager != null)
        {
            cycleManager.OnDayReady.AddListener(HandleDayStart);
        }
    }

    private void OnDestroy()
    {
        if (Cycle != null)
        {
            Cycle.OnDayReady.RemoveListener(HandleDayStart);
        }
    }

    // Dragon.OnDragonTypeChanged가 현재 한 번도 invoke되지 않으므로(RunData.cs 실측),
    // 속성 변경 UI가 TryChangeType 성공 직후 이 메서드를 호출해 알려야 한다.
    public void NotifyActiveAttributeChanged()
    {
        DragonType? active = ActiveAttribute;
        if (active.HasValue)
        {
            _activeAttributeChanged.Invoke(active.Value);
        }
    }

    private void HandleDayStart(int day)
    {
        NotifyActiveAttributeChanged();
    }
}
