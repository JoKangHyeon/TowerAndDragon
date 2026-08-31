using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 튜토리얼 중도 포기. 그만두기 버튼 하나와 확인 팝업으로 이루어진다.
///
/// 포기의 뜻은 "안내만 끄기"가 아니라 <b>튜토리얼을 끝내고 본게임을 새로 시작</b>이다.
/// 튜토리얼 씬은 3일짜리 축소판이고 마지막 밤은 이길 수 없게 짜여 있어, 안내만 꺼 두면
/// 플레이어는 목적지가 없는 맵에 남는다. 그래서 엔딩이 끝났을 때와 같은 곳으로 보낸다 -
/// <see cref="TutorialToGameHandoff"/>와 같은 경로이고, 정적 상태를 아무것도 세팅하지 않는 것이 계약이다
/// (SaveLoadRequest가 비어 있어야 GameManager가 처음 게임을 켠 것과 같은 경로로 간다).
///
/// <b>배치 규칙:</b> 이 패널은 <see cref="UI_GuideOverlay"/>의 딤보다 <b>뒤 형제</b>여야 한다.
/// 안내 단계 대부분이 입력을 막으므로, 딤 앞에 두면 정작 그만두고 싶은 순간에 버튼이 눌리지 않는다
/// (목표 패널이 뒤 형제여야 하는 것과 같은 이유). 반대로 엔딩 패널보다는 앞 형제여야 컷씬을 가리지 않는다.
/// </summary>
public sealed class UI_TutorialQuitPanel : MonoBehaviour
{
    [Tooltip("항상 보이는 그만두기 버튼. 누르면 확인 팝업을 연다.")]
    [SerializeField] private Button _quitButton;

    [Tooltip("확인 팝업 루트. 뒤쪽 입력을 막는 전체 화면 이미지를 포함해야 한다.")]
    [SerializeField] private GameObject _confirmPopup;

    [Tooltip("정말 그만둔다. 본게임 씬으로 넘어간다.")]
    [SerializeField] private Button _confirmButton;

    [Tooltip("팝업만 닫고 튜토리얼을 계속한다.")]
    [SerializeField] private Button _cancelButton;

    [Tooltip("본게임 씬을 여는 동안 화면을 덮는 로딩 화면. 비어 있으면 로딩 화면 없이 바로 넘어간다.")]
    [SerializeField] private SceneLoadOverlay _loadOverlay;

    // 씬 로드는 되돌릴 수 없으므로 두 번 눌려도 한 번만 나간다(TutorialToGameHandoff와 같은 방어).
    private bool _hasRequested;

    private void Awake()
    {
        if (_quitButton != null)
        {
            _quitButton.onClick.AddListener(OpenConfirm);
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(QuitToGame);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.AddListener(CloseConfirm);
        }

        // 팝업은 자기 자신을 닫는 Awake를 가진 창이 아니라 평범한 자식 오브젝트이므로,
        // CLAUDE.md의 _isOpen 가드가 필요 없다 - 여기서 한 번 닫아 두면 그대로 닫힌 채 시작한다.
        CloseConfirmSilently();
    }

    private void OnDestroy()
    {
        if (_quitButton != null)
        {
            _quitButton.onClick.RemoveListener(OpenConfirm);
        }

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(QuitToGame);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(CloseConfirm);
        }
    }

    public void OpenConfirm()
    {
        if (_confirmPopup != null)
        {
            SoundManager.Play(SoundId.UiButtonClick);
            _confirmPopup.SetActive(true);
        }
    }

    public void CloseConfirm()
    {
        if (_confirmPopup != null)
        {
            SoundManager.Play(SoundId.UiButtonClick);
            _confirmPopup.SetActive(false);
        }
    }

    // Awake의 초기 닫기가 소리를 내지 않도록 소리 없는 경로를 따로 둔다 - 그냥 CloseConfirm을 부르면
    // 튜토리얼 씬에 들어오자마자 취소 버튼을 누른 소리가 난다(UI_TutorialPromptPanel.CloseSilently와 같은 이유).
    private void CloseConfirmSilently()
    {
        if (_confirmPopup != null)
        {
            _confirmPopup.SetActive(false);
        }
    }

    /// <summary>
    /// 튜토리얼을 끝내고 본게임으로 나간다. Time.timeScale은 여기서 되돌리지 않는다 -
    /// 씬이 내려갈 때 GameSpeedManager가, 새 씬에서 다시 GameSpeedManager가 1로 복구한다.
    /// 복구 지점을 늘리면 순서 경쟁만 생긴다(TutorialToGameHandoff와 같은 판단).
    /// </summary>
    public void QuitToGame()
    {
        if (_hasRequested)
        {
            return;
        }

        _hasRequested = true;

        // 두 번째 클릭은 위 가드에서 걸러지므로, 소리도 실제로 나가는 첫 클릭에서만 낸다.
        SoundManager.Play(SoundId.UiButtonClick);
        Debug.Log("[UI_TutorialQuitPanel] 튜토리얼을 중도 포기하고 본게임으로 넘어갑니다.", this);

        SceneLoadOverlay.LoadOrFallback(_loadOverlay, SceneNames.SAMPLE_GAME, nameof(_loadOverlay), this);
    }
}
