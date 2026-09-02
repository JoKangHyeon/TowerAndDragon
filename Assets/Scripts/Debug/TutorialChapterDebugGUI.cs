using UnityEngine;

#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [에디터 테스트 전용] 튜토리얼 챕터를 건너뛰거나 원하는 챕터로 곧바로 옮긴다.
/// 마일스톤이 쌓이면서 뒤쪽 챕터 하나를 보려고 앞을 전부 플레이해야 하는 시간이 길어져서 만들었다.
/// ResearchLabDebugGUI·DragonTreeDebugGUI와 같은 IMGUI 관례를 따른다.
///
/// F9로 여닫는다 - 항상 떠 있으면 정작 확인하려는 안내 말풍선과 딤을 가린다.
/// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
/// (클래스 껍데기만 남아 씬의 컴포넌트 참조가 Missing Script가 되지 않는다).
/// </summary>
public sealed class TutorialChapterDebugGUI : MonoBehaviour
{
#if UNITY_EDITOR
    private const float PANEL_MARGIN = 10f;
    private const float PANEL_WIDTH = 380f;
    private const float PANEL_MAX_HEIGHT = 520f;
    private const int SECTION_SPACE = 8;

    [Tooltip("챕터 목록과 전환을 다루는 컨트롤러. 비우면 씬에서 찾는다.")]
    [WiringOptional]
    [SerializeField] private TutorialScenarioController _scenarioController;

    [Tooltip("밤으로 넘겨 다음 일차의 챕터를 여는 데 쓴다. 비우면 씬에서 찾는다.")]
    [WiringOptional]
    [SerializeField] private CycleManager _cycleManager;

    [Tooltip("남은 목표를 보고 한 번에 채우는 데 쓴다. 비우면 씬에서 찾는다.")]
    [WiringOptional]
    [SerializeField] private TutorialObjectiveController _objectiveController;

    [Tooltip("켜 두면 씬을 시작할 때부터 패널이 떠 있다. 평소에는 꺼 두고 F9로 연다.")]
    [SerializeField] private bool _isVisible;

    private Vector2 _scroll;

    private void Awake()
    {
        // 인스펙터를 비워 둬도 쓸 수 있게 한다 - 디버그 도구를 여러 씬에 붙일 때 배선이 번거롭다.
        _scenarioController ??= FindFirstObjectByType<TutorialScenarioController>(FindObjectsInactive.Include);
        _cycleManager ??= FindFirstObjectByType<CycleManager>(FindObjectsInactive.Include);
        _objectiveController ??=
            FindFirstObjectByType<TutorialObjectiveController>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            _isVisible = !_isVisible;
        }
    }

    private void OnGUI()
    {
        if (!_isVisible || _scenarioController == null)
        {
            return;
        }

        float height = Mathf.Min(PANEL_MAX_HEIGHT, Screen.height - PANEL_MARGIN * 2f);
        GUILayout.BeginArea(
            new Rect(Screen.width - PANEL_WIDTH - PANEL_MARGIN, PANEL_MARGIN, PANEL_WIDTH, height),
            GUI.skin.box);

        GUILayout.Label("Tutorial Chapters (Debug) - F9 to hide");
        DrawStatus();

        GUILayout.Space(SECTION_SPACE);
        DrawActions();

        GUILayout.Space(SECTION_SPACE);
        GUILayout.Label("Jump to chapter:");
        _scroll = GUILayout.BeginScrollView(_scroll);
        DrawChapterList();
        GUILayout.EndScrollView();

        GUILayout.EndArea();
    }

    private void DrawStatus()
    {
        int day = _cycleManager == null ? 0 : _cycleManager.CurrentDayNumber;
        TutorialRunner current = _scenarioController.DebugCurrentRunner;
        string currentName = current == null ? "(none)" : current.name;
        string running = current != null && current.isActiveAndEnabled ? "running" : "stopped";

        GUILayout.Label($"Day {day} | index {_scenarioController.DebugCurrentIndex}/{_scenarioController.DebugChapterCount - 1}");
        GUILayout.Label($"Current: {currentName} ({running})");

        if (_objectiveController != null)
        {
            // 챕터를 건너뛰면 앞 챕터의 목표가 비어 있어 밤 진입과 보스 준비 안내가 열리지 않는다 -
            // "안 뜬다"는 제보의 흔한 원인이라 남은 수를 항상 보여준다.
            GUILayout.Label($"Objectives left today: {_objectiveController.DebugRemainingObjectiveCount}");
        }

        if (_scenarioController.DebugHasPendingChapter)
        {
            // 대기 중이면 아무 챕터도 돌지 않는다 - 안내가 안 뜬다는 제보의 흔한 원인이라 눈에 띄게 적는다.
            GUILayout.Label($"PENDING: waits for day {_scenarioController.DebugCurrentChapterStartDay}");
        }
    }

    private void DrawActions()
    {
        TutorialRunner current = _scenarioController.DebugCurrentRunner;

        // Skip은 TutorialEnded를 발화하는 정상 경로라, 그 신호에 매달린 것들(알 지급 등)이 함께 돈다.
        // 그래서 "다음 챕터로"는 점프가 아니라 이쪽을 쓴다.
        GUI.enabled = current != null && current.isActiveAndEnabled;
        if (GUILayout.Button("Skip current chapter (-> next)"))
        {
            Debug.Log($"[TutorialChapterDebugGUI] 챕터 건너뛰기: {current.name}", current);
            current.Skip();
        }

        GUI.enabled = true;

        GUI.enabled = _objectiveController != null;
        if (GUILayout.Button("Complete today's objectives"))
        {
            Debug.Log("[TutorialChapterDebugGUI] 오늘 목록의 목표를 모두 완료 처리합니다.", this);
            _objectiveController.DebugCompleteVisibleObjectives();
        }

        GUI.enabled = true;

        GUI.enabled = _cycleManager != null;
        if (GUILayout.Button("Force end day (-> night)"))
        {
            Debug.Log("[TutorialChapterDebugGUI] 관문을 무시하고 밤으로 넘깁니다.", this);
            _cycleManager.ForceEndDay();
        }

        GUI.enabled = true;
        GUILayout.Label("(night: press F to finish the wave)");
    }

    private void DrawChapterList()
    {
        for (int i = 0; i < _scenarioController.DebugChapterCount; i++)
        {
            TutorialRunner runner = _scenarioController.DebugGetRunner(i);
            string label = runner == null ? "(empty)" : runner.name;
            string marker = i == _scenarioController.DebugCurrentIndex ? "> " : "  ";

            GUI.enabled = runner != null && i != _scenarioController.DebugCurrentIndex;
            if (GUILayout.Button($"{marker}{i}. {label} (D{_scenarioController.DebugGetStartDay(i)})"))
            {
                Debug.Log($"[TutorialChapterDebugGUI] 챕터 {i}({label})로 이동합니다.", this);
                _scenarioController.DebugJumpToChapter(i);
            }
        }

        GUI.enabled = true;
    }
#endif
}
