using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 게임오버 창 컨트롤러. 이 창에 부착해, 창 안의 UI 요소(재시작 버튼 등)를 한곳에서 관리한다.
/// 창의 표시/숨김은 UIManager가 담당하고, 이 스크립트는 창 내부 동작만 맡는다.
/// </summary>
public class UI_GameOverWindow : MonoBehaviour
{
    [SerializeField] private Button _restartButton;

    private void Awake()
    {
        if (_restartButton != null)
        {
            _restartButton.onClick.AddListener(RestartScene);
        }
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
