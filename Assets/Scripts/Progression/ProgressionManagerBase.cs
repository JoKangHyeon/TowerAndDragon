using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// 연구·용 스킬트리 등 "선행 + 자원 코스트로 노드를 해금하는 트리" 공통 매니저 베이스.
// 판정은 전부 ProgressionRules.Evaluate에 위임하고, 여기서는 참조 캐시·차감·이벤트만 담당한다.
// (ResearchManager.TryResearch와 동일 철학: GetNodeState 재평가 -> Available일 때만 변경.)
public abstract class ProgressionManagerBase : MonoBehaviour, IProgressionState
{
    [SerializeField] private UnityEvent<ProgressionNodeData> _nodeUnlocked = new();

    private readonly ProgressionTreeState _state = new();
    private bool _isConstructed;

    // 프로퍼티 이름을 타입명(CycleManager/ResourceManager)과 다르게 둔다 -
    // 동일하게 두면 "CycleManager.CycleState.Day"처럼 타입 이름이 필요한 자리에서
    // 프로퍼티가 타입을 가려 컴파일 오류가 난다.
    protected GameManager Game { get; private set; }
    protected CycleManager Cycle { get; private set; }
    protected ResourceManager Resources { get; private set; }

    protected abstract ProgressionTreeData TreeData { get; }

    public UnityEvent<ProgressionNodeData> NodeUnlocked => _nodeUnlocked;

    // --- IProgressionState (게이트가 완료 집합을 읽는 창구) ---
    public bool IsUnlocked(string nodeId) => _state.IsUnlocked(nodeId);
    public IReadOnlyCollection<string> UnlockedIds => _state.UnlockedIds;
    public bool TryGetNode(string nodeId, out ProgressionNodeData node) => _state.TryGetNode(nodeId, out node);

    protected void ConstructBase(GameManager game, CycleManager cycle, ResourceManager resources)
    {
        if (_isConstructed)
        {
            return;
        }

        Game = game;
        Cycle = cycle;
        Resources = resources;
        _state.CacheNodes(TreeData);
        _isConstructed = true;
    }

    // 파생 시스템 전용 비용(연구 RP 등) 훅. 기본은 통과.
    protected virtual ProgressionNodeState CheckExtraCost(ProgressionNodeData node) => ProgressionNodeState.Available;
    protected virtual void PayExtraCost(ProgressionNodeData node) { }
    protected virtual void OnNodeUnlocked(ProgressionNodeData node) { }
    protected virtual ProgressionContext BuildGateContext() => new ProgressionContext(Game, Cycle, this);

    protected bool IsDay() => Cycle != null && Cycle.CurrentCycle == CycleManager.CycleState.Day;

    public ProgressionNodeState GetNodeState(ProgressionNodeData node)
    {
        Func<IReadOnlyList<ResourceAmount>, bool> canAfford =
            Resources != null ? Resources.CanAfford : (Func<IReadOnlyList<ResourceAmount>, bool>)null;

        return ProgressionRules.Evaluate(
            node,
            this,
            IsDay(),
            canAfford,
            BuildGateContext(),
            CheckExtraCost);
    }

    // UI가 GateLocked 상태의 표시 문구를 얻기 위한 헬퍼 - 첫 번째로 불만족한 게이트의 LockedLocKey를 반환한다.
    public string GetFirstFailingGateLocKey(ProgressionNodeData node)
    {
        if (node == null)
        {
            return null;
        }

        ProgressionContext context = BuildGateContext();
        foreach (ProgressionGateSO gate in node.Gates)
        {
            if (gate != null && !gate.IsSatisfied(context))
            {
                return gate.LockedLocKey;
            }
        }

        return null;
    }

    public bool TryUnlock(ProgressionNodeData node, out ProgressionFailureReason reason)
    {
        ProgressionNodeState nodeState = GetNodeState(node);
        reason = ProgressionRules.ToFailureReason(nodeState);

        if (nodeState != ProgressionNodeState.Available)
        {
            return false;
        }

        Resources.Spend(node.ResourceCost);
        PayExtraCost(node);
        _state.MarkUnlocked(node.NodeId);

        OnNodeUnlocked(node);
        _nodeUnlocked.Invoke(node);
        reason = ProgressionFailureReason.None;
        return true;
    }
}
