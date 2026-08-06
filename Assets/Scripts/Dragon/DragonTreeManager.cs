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

    [SerializeField] private DragonSkillTreeData _tree;
    [SerializeField] private UnityEvent<DragonType> _activeAttributeChanged = new();

    protected override ProgressionTreeData TreeData => _tree;

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

    public StatusEffectSO GetSpawnStatus(DragonType activeAttribute)
    {
        foreach (string nodeId in UnlockedIds)
        {
            if (!TryGetNode(nodeId, out ProgressionNodeData node) || !(node is DragonSkillNodeData dragonNode))
            {
                continue;
            }

            foreach (DragonSkillEffectSO effect in dragonNode.Effects)
            {
                StatusEffectSO status = effect != null ? effect.GetSpawnStatus(activeAttribute) : null;

                if (status != null)
                {
                    return status;
                }
            }
        }

        return null;
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

    // 어떤 용 노드도 사거리를 다루지 않는다(로드맵 §10 노드 표) - 중립값 고정.
    public float GetRangeMultiplier(TowerData towerData) => BASE_RANGE_MULTIPLIER;

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

    private StatusEffectSO FindTowerHitStatus(TowerData towerData, bool requireBabyDragonTarget)
    {
        DragonType? active = ActiveAttribute;

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

                if (status != null)
                {
                    return status;
                }
            }
        }

        return null;
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
