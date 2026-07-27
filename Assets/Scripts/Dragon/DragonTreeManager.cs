using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 용 스킬트리 매니저. ResearchManager와 대칭 구조이되 공용 판정(선행·게이트·자원)은
// ProgressionManagerBase에 위임한다.
// 이름을 로드맵 문서의 DragonSkillManager 대신 DragonTreeManager로 둔 이유:
// 기존 전투 액티브 스킬 매니저 Assets/Scripts/UI/Skill/SkillManager.cs와의 이름 충돌 회피
// (Build3_프로토타입_일정계획.md 리스크 항목).
public sealed class DragonTreeManager : ProgressionManagerBase
{
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

    // 액티브 스킬 해금 pull API. SkillManager는 이번 마일스톤에서 수정하지 않으므로
    // 해금된 노드가 참조하는 SkillSO 목록만 노출한다(SkillManager 연결은 알파 이후 - 비목표).
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
                    if (effect is ActiveSkillUnlockEffectSO activeSkillEffect && activeSkillEffect.Skill != null)
                    {
                        yield return activeSkillEffect.Skill;
                    }
                }
            }
        }
    }

    public void Construct(GameManager gameManager, CycleManager cycleManager, ResourceManager resourceManager)
    {
        ConstructBase(gameManager, cycleManager, resourceManager);

        if (cycleManager != null)
        {
            cycleManager.OnDayStart.AddListener(HandleDayStart);
        }
    }

    private void OnDestroy()
    {
        if (Cycle != null)
        {
            Cycle.OnDayStart.RemoveListener(HandleDayStart);
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
