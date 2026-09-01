using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 게임오버 창 컨트롤러. 이 창에 부착해, 창 안의 UI 요소(타이틀 복귀 버튼 등)를 한곳에서 관리한다.
/// 창의 표시/숨김은 UIManager가 담당하고, 이 스크립트는 창 내부 동작만 맡는다.
/// 승리 창(Victory_window)도 같은 스크립트를 쓰므로, 한쪽에만 있는 요소는 비워 둘 수 있게 둔다.
/// </summary>
public class UI_GameOverWindow : MonoBehaviour
{
    [Tooltip("타이틀 화면(StartScene)으로 나가는 버튼. 라벨은 LocalizedText의 gameOver_button 키가 채운다.")]
    [FormerlySerializedAs("_restartButton")]
    [SerializeField] private Button _toTitleButton;

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

    // 씬 교체를 이미 요청했는지. 씬은 프레임 끝에 바뀌므로 그 전까지 버튼이 계속 눌린다.
    private bool _hasRequestedSceneChange;

    private void Awake()
    {
        if (_toTitleButton != null)
        {
            _toTitleButton.onClick.AddListener(ReturnToTitle);
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

    /// <summary>이 런을 끝내고 타이틀 화면으로 나간다. 승리 창·패배 창이 같은 동작을 쓴다.</summary>
    // 예전에는 현재 씬을 그대로 다시 열어(= 곧바로 새 런 시작) 재시작했지만, 승패가 갈린 뒤에
    // 곧장 다음 판으로 던져 넣는 대신 타이틀로 돌려보내 새 게임/이어하기를 고르게 한다.
    // 목적지는 인게임 설정 창의 "메인 화면으로 돌아가기"(UI_ConfigWindow.ReturnToTitle)와 같다.
    //
    // Time.timeScale은 여기서 건드리지 않는다 - 엔딩 창은 GameSpeedManager가 0으로 못박은 뒤에 열리지만,
    // 씬이 내려갈 때 GameSpeedManager.OnDisable이 1로 되돌린다.
    // 곧바로 씬이 언로드되므로 클릭음은 거의 들리지 않지만, 무음보다는 낫다.
    private void ReturnToTitle()
    {
        // 씬 교체는 프레임 끝에 일어난다. 그 사이 연타가 로드를 두 번 걸지 않도록 한 번만 받는다.
        if (_hasRequestedSceneChange)
        {
            return;
        }

        _hasRequestedSceneChange = true;

        SoundManager.Play(SoundId.UiButtonClick);

        SceneManager.LoadScene(SceneNames.START);
    }
}
