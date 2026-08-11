using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 엔딩이 끝나면 본게임으로 넘긴다. 정적 상태를 아무것도 세팅하지 않는 것이 핵심이다 -
/// SaveLoadRequest가 비어 있어야 GameManager.Start가 StartNewRun()으로 가고,
/// 처음 게임을 켰을 때와 완전히 같은 경로가 된다.
///
/// Time.timeScale은 여기서 되돌리지 않는다. 씬이 언로드될 때 GameSpeedManager.OnDisable이,
/// 새 씬에서는 GameSpeedManager.Awake가 이미 1로 복구한다 - 복구 지점을 늘리면 순서 경쟁만 생긴다.
/// </summary>
public sealed class TutorialToGameHandoff : MonoBehaviour
{
    [SerializeField] private TutorialEndingSequencer _sequencer;

    // 씬 로드는 되돌릴 수 없으므로 두 번 불려도 한 번만 나간다.
    private bool _hasRequested;

    private void OnEnable()
    {
        if (_sequencer == null)
        {
            Debug.LogError("[TutorialToGameHandoff] TutorialEndingSequencer 참조가 없습니다.", this);
            return;
        }

        _sequencer.EndingCompleted.AddListener(HandleEndingCompleted);
    }

    private void OnDisable()
    {
        if (_sequencer != null)
        {
            _sequencer.EndingCompleted.RemoveListener(HandleEndingCompleted);
        }
    }

    private void HandleEndingCompleted()
    {
        if (_hasRequested)
        {
            return;
        }

        _hasRequested = true;
        SceneManager.LoadScene(SceneNames.SAMPLE_GAME);
    }
}
