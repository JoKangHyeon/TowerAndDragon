using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 튜토리얼이 시작되면 카메라를 움직일 수 있다는 것을 토스트로 한 번 알린다.
///
/// 안내 단계(TutorialStepSO)로 만들지 않는 이유: 이것은 "지금 해야 할 일"이 아니라 상시 조작이다.
/// 컷으로 넣으면 확인 버튼을 눌러야 넘어가는 관문이 하나 더 생기고, 그 컷의 딤이 화면을 덮는 동안
/// 정작 움직여 보라고 한 화면이 가려진다.
///
/// 알려야 하는 이유: 말풍선이 그리드의 일부를 가리는데(배치 단계가 그렇다), 화면을 움직일 수 있다는 것을
/// 모르면 가려진 만큼이 그냥 "지을 수 없는 땅"으로 읽힌다.
/// 카메라는 어느 관문에도 걸려 있지 않아 딤이 깔린 컷에서도 그대로 움직인다 - 없는 것은 기능이 아니라 안내였다.
/// </summary>
public sealed class TutorialCameraHintToast : MonoBehaviour
{
    private const string CAMERA_HINT_LOC_KEY = "tutorial_camera_hint";
    private const float DEFAULT_DELAY_SECONDS = 1.5f;
    private const float DEFAULT_SHOW_SECONDS = 4f;

    [Tooltip("문구를 띄울 토스트. 비우면 아무것도 하지 않는다.")]
    [SerializeField] private UI_NotificationToast _toast;

    [Tooltip("씬이 시작되고 이만큼 뒤에 띄운다. 0이면 첫 안내 말풍선이 뜨는 순간과 겹쳐 둘 다 읽히지 않는다.")]
    [Min(0f)]
    [SerializeField] private float _delaySeconds = DEFAULT_DELAY_SECONDS;

    [Tooltip("화면에 머무는 시간(초). 토스트 기본값보다 길게 잡는다 - 한 번 읽고 외워야 하는 조작 안내다.")]
    [Min(0f)]
    [SerializeField] private float _showSeconds = DEFAULT_SHOW_SECONDS;

    private void Start()
    {
        ShowLaterAsync().Forget();
    }

    private async UniTaskVoid ShowLaterAsync()
    {
        if (_toast == null)
        {
            Debug.LogWarning("[TutorialCameraHintToast] 토스트가 비어 있어 카메라 안내를 띄우지 못합니다.", this);
            return;
        }

        // 일시정지 중에도 흘러야 한다 - 안내 말풍선·토스트가 모두 unscaled로 도는 것과 짝이다.
        await UniTask.WaitForSeconds(
            _delaySeconds,
            ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy());

        _toast.ShowFor(_showSeconds, CAMERA_HINT_LOC_KEY);
    }
}
