using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [에디터 테스트 전용] 플레이 모드에서 I 키를 누르면 연구 점수를 지급한다.
///
/// O 치트(<see cref="EditorResourceResearchCheatController"/>)와 나눠 둔 이유:
/// 그쪽은 노드를 통째로 완료시켜 버려 "RP를 모아 하나씩 연구하는" 흐름을 볼 수 없다.
/// 연구 창을 열어 둔 채 이 키를 누르면 노드가 잠금 → 연구 가능으로 바뀌는 것과
/// 연결선이 차오르는 연출을 그대로 확인할 수 있다.
/// </summary>
public sealed class EditorResearchPointCheatController : MonoBehaviour
{
#if UNITY_EDITOR
    private const int CHEAT_RESEARCH_POINT_AMOUNT = 100;

    private static EditorResearchPointCheatController _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        var root = new GameObject(nameof(EditorResearchPointCheatController));
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<EditorResearchPointCheatController>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.iKey.wasPressedThisFrame)
        {
            return;
        }

        ApplyCheat();
    }

    private void ApplyCheat()
    {
        GameManager gameManager = FindFirstObjectByType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogWarning("[EditorResearchPointCheatController] GameManager를 찾지 못해 I 치트를 적용하지 않았습니다.", this);
            return;
        }

        ResearchManager researchManager = gameManager.ResearchManager;

        if (researchManager == null)
        {
            Debug.LogWarning("[EditorResearchPointCheatController] ResearchManager가 없어 I 치트를 적용하지 않았습니다.", this);
            return;
        }

        researchManager.AddResearchPoints(CHEAT_RESEARCH_POINT_AMOUNT);

        Debug.Log(
            $"[EditorResearchPointCheatController] I 치트 적용: 연구 점수 +{CHEAT_RESEARCH_POINT_AMOUNT} " +
            $"(현재 {researchManager.ResearchPoints})",
            this);
    }
#endif
}
