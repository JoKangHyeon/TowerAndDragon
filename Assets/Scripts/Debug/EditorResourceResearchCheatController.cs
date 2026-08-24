using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [에디터 테스트 전용] 플레이 모드에서 O 키를 누르면 모든 자원을 지급하고 모든 연구를 해금한다.
/// </summary>
public sealed class EditorResourceResearchCheatController : MonoBehaviour
{
#if UNITY_EDITOR
    private const int CHEAT_RESOURCE_AMOUNT = 500;

    private static EditorResourceResearchCheatController _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        var root = new GameObject(nameof(EditorResourceResearchCheatController));
        DontDestroyOnLoad(root);
        _instance = root.AddComponent<EditorResourceResearchCheatController>();
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
        if (Keyboard.current == null || !Keyboard.current.oKey.wasPressedThisFrame)
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
            Debug.LogWarning("[EditorResourceResearchCheatController] GameManager를 찾지 못해 O 치트를 적용하지 않았습니다.", this);
            return;
        }

        int addedResourceCount = gameManager.ResourceManager != null
            ? gameManager.ResourceManager.DebugAddToAllCatalogResources(CHEAT_RESOURCE_AMOUNT)
            : 0;
        int completedResearchCount = gameManager.ResearchManager != null
            ? gameManager.ResearchManager.DebugCompleteAllResearchNodes()
            : 0;

        Debug.Log(
            $"[EditorResourceResearchCheatController] O 치트 적용: 자원 {addedResourceCount}종 +{CHEAT_RESOURCE_AMOUNT}, " +
            $"연구 {completedResearchCount}개 신규 해금",
            this);
    }
#endif
}
