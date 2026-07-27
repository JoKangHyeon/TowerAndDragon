using UnityEngine;

// 임시 디버그 GUI - 용 스킬트리 노드 상태 확인·해금 시도·테스트용 자원 지급.
// ResearchLabDebugGUI와 동일한 IMGUI 관례(TowerAndDragon/Debug). 새끼용(#102·#103)이
// 아직 없어 게이트 검증도 여기서 겸한다(강화/궁극은 임시로 항상 통과).
// 제출 전 B-2(디버그 UI 비노출) 대상에 이 컴포넌트를 추가해야 한다.
public class DragonTreeDebugGUI : MonoBehaviour
{
    private const int TEST_GRANT_AMOUNT = 999;

    private static readonly ResourceType[] TEST_RESOURCE_TYPES =
    {
        ResourceType.GrassSlime, ResourceType.RockSlime, ResourceType.VolcanoSlime,
        ResourceType.DesertSlime, ResourceType.SnowSlime,
        ResourceType.FlameHeart, ResourceType.SnowCrystal, ResourceType.TimeSand, ResourceType.PhilosopherStone,
    };

    [SerializeField] private DragonTreeManager _dragonTreeManager;
    [SerializeField] private ResourceManager _resourceManager;

    // UI_MainCastleWindow._dragonSkillTreeButton이 아직 씬에 없어(C-1/C-2 미완) 임시로 여기서 연다.
    // C-1/C-2 완료 후 정식 진입점이 연결되면 이 필드는 제거한다.
    [SerializeField] private UI_DragonSkillWindow _dragonSkillWindow;

    private Vector2 _scroll;

    private void OnGUI()
    {
        if (_dragonTreeManager == null || _dragonTreeManager.Tree == null)
        {
            return;
        }

        GUILayout.BeginArea(new Rect(10, 10, 460, Screen.height - 20), GUI.skin.box);
        GUILayout.Label("Dragon Skill Tree (Debug)");

        string activeAttribute = _dragonTreeManager.ActiveAttribute.HasValue
            ? _dragonTreeManager.ActiveAttribute.Value.ToString()
            : "None (CurrentDragon null)";
        GUILayout.Label($"Active Attribute: {activeAttribute}");
        GUILayout.Label($"Unlocked Kin Count: {_dragonTreeManager.UnlockedKinCount}");

        if (GUILayout.Button($"Grant {TEST_GRANT_AMOUNT} of every slime/specialized resource"))
        {
            GrantTestResources();
        }

        if (_dragonSkillWindow != null && GUILayout.Button("Open Dragon Skill Tree Window"))
        {
            _dragonSkillWindow.ToggleFromEntryPoint();
        }

        GUILayout.Space(8);
        _scroll = GUILayout.BeginScrollView(_scroll);

        foreach (DragonSkillNodeData node in _dragonTreeManager.Tree.DragonNodes)
        {
            DrawNode(node);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawNode(DragonSkillNodeData node)
    {
        ProgressionNodeState state = _dragonTreeManager.GetNodeState(node);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"[{node.Attribute}] {node.NodeId} - {state}", GUILayout.Width(340));

        GUI.enabled = state == ProgressionNodeState.Available;
        if (GUILayout.Button("Unlock"))
        {
            _dragonTreeManager.TryUnlock(node, out ProgressionFailureReason _);
        }
        GUI.enabled = true;

        GUILayout.EndHorizontal();
    }

    private void GrantTestResources()
    {
        if (_resourceManager == null)
        {
            return;
        }

        foreach (ResourceType type in TEST_RESOURCE_TYPES)
        {
            _resourceManager.Add(type, TEST_GRANT_AMOUNT);
        }
    }
}
