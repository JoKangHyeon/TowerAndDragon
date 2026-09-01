using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임오버 창 컨트롤러. 이 창에 부착해, 창 안의 UI 요소(재시작 버튼 등)를 한곳에서 관리한다.
/// 창의 표시/숨김은 UIManager가 담당하고, 이 스크립트는 창 내부 동작만 맡는다.
/// 승리 창(Victory_window)도 같은 스크립트를 쓰므로, 한쪽에만 있는 요소는 비워 둘 수 있게 둔다.
/// </summary>
public class UI_GameOverWindow : MonoBehaviour
{
    [SerializeField] private Button _restartButton;

    [Tooltip("최종 버틴 날짜(Result/Result_amount). 없으면 표시를 생략한다.")]
    [SerializeField]
    [WiringOptional]
    private TMP_Text _survivedDayText;

    [Tooltip("최종 일차를 읽어 올 대상. 씬 오브젝트라 프리팹이 아니라 씬에서 배선한다.")]
    [SerializeField]
    [WiringOptional]
    private CycleManager _cycleManager;

    [Tooltip("철인 모드에서 세이브가 삭제됐음을 알리는 줄. 없으면 안내만 생략된다.")]
    [SerializeField]
    [WiringOptional]
    private LocalizedText _ironmanDeletedLabel;

    [Tooltip("철인 모드 세이브 삭제 여부를 묻는 대상. 없으면 안내를 띄우지 않는다.")]
    [SerializeField]
    [WiringOptional]
    private SaveService _saveService;

    private void Awake()
    {
        if (_restartButton != null)
        {
            _restartButton.onClick.AddListener(RestartScene);
        }
    }

    private void OnEnable()
    {
        if (_saveService != null)
        {
            _saveService.IronmanSaveDeleted.AddListener(RenderIronmanNotice);
        }

        // 이 창은 비활성으로 저장돼 있어 삭제 시점에는 OnEnable이 아직 돌지 않는다 -
        // 이벤트만 기다리면 매번 놓친다. 구독 직후 현재 값을 한 번 반영한다(CLAUDE.md 이벤트 규칙).
        RenderIronmanNotice();

        RenderSurvivedDay();
    }

    private void OnDisable()
    {
        if (_saveService != null)
        {
            _saveService.IronmanSaveDeleted.RemoveListener(RenderIronmanNotice);
        }
    }

    private void RenderIronmanNotice()
    {
        if (_ironmanDeletedLabel == null)
        {
            return;
        }

        bool wasDeleted = _saveService != null && _saveService.WasIronmanSaveDeleted;

        // key를 씬에 박아 두지 않고 코드에서 넣는다 - 오타가 컴파일 시점에 드러난다.
        if (wasDeleted)
        {
            _ironmanDeletedLabel.SetKey(SaveLocKeys.IRONMAN_SAVE_DELETED);
        }

        _ironmanDeletedLabel.gameObject.SetActive(wasDeleted);
    }

    // 창이 열리는 시점의 누적 일차를 인게임 창 날짜 표기와 같은 서식("0주기 1일차")으로 보여 준다.
    // 게임은 이미 끝나 일차가 더 늘지 않으므로, 이벤트를 구독하지 않고 열릴 때 한 번만 읽는다.
    private void RenderSurvivedDay()
    {
        if (_survivedDayText == null || _cycleManager == null)
        {
            return;
        }

        _survivedDayText.text = CycleCalendar.FormatDayLabel(_cycleManager.CurrentDayNumber);
    }

    // 현재 씬을 다시 로드해 게임을 재시작한다(현재 씬이 Build Settings에 등록돼 있어야 함).
    // 곧바로 씬이 언로드되므로 클릭음은 거의 들리지 않지만, 무음보다는 낫다.
    private void RestartScene()
    {
        SoundManager.Play(SoundId.UiButtonClick);

        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }
}
