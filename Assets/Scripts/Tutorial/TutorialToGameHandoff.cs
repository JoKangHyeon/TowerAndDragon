using UnityEngine;

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

    [Tooltip("본게임 씬을 여는 동안 화면을 덮는 로딩 화면. 비어 있으면 로딩 화면 없이 바로 넘어간다.")]
    [SerializeField] private SceneLoadOverlay _loadOverlay;

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

        // 시퀀서가 컷씬 패널을 걷은 뒤에 이 이벤트가 오므로, 여기서 덮기 전까지 튜토리얼 맵이 잠깐 보인다.
        // 그 깜빡임을 줄이려면 씬의 오버레이 인스턴스에서 페이드 인 시간을 0으로 둔다.
        SceneLoadOverlay.LoadOrFallback(_loadOverlay, SceneNames.SAMPLE_GAME, nameof(_loadOverlay), this);
    }
}
