using DG.Tweening;
using UnityEngine;

/// <summary>
/// 시계 초침처럼 자기 자신을 계속 회전시킨다. 엔딩 컷씬의 연출 프리팹(TutorialEndingBeatSO._effectPrefab)에
/// 붙여 쓴다 - 컷씬은 timeScale이 0으로 굳은 화면에서 재생되므로 스케일을 무시하고 돈다.
///
/// 회전축은 이 오브젝트의 RectTransform pivot이다. 초침 스프라이트는 회전 중심(축 보석)이 이미지
/// 정중앙이 아니라 아래쪽에 있으므로, pivot을 그 지점으로 옮겨야 시계 밖으로 휘둘리지 않는다.
/// time_clock_second_hand(1254x1254)의 보석 중심은 실측 pivot (0.5, 0.331)이다.
/// 시계판(time_clock_face)은 축이 정중앙이라 pivot을 건드리지 않는다.
/// </summary>
public sealed class UI_RotatingHand : MonoBehaviour
{
    private const float FULL_TURN_DEGREES = 360f;
    private const float MIN_TURN_SECONDS = 0.01f;

    [Tooltip("한 바퀴 도는 데 걸리는 시간(초).")]
    [Min(MIN_TURN_SECONDS)]
    [SerializeField] private float _secondsPerTurn = 3f;

    [Tooltip("시계 반대 방향으로 돈다. 시간이 되돌아가는 연출에 쓴다.")]
    [SerializeField] private bool _isCounterClockwise = true;

    [Tooltip("느리게 시작해 점점 빨라진다. 되감기가 가속되는 느낌을 준다. 끄면 일정한 속도로 돈다.")]
    [SerializeField] private bool _acceleratesOverTime;

    [Tooltip("가속을 켰을 때 마지막 회전이 첫 회전보다 몇 배 빠른지.")]
    [Min(1f)]
    [SerializeField] private float _finalSpeedMultiplier = 4f;

    [Tooltip("가속에 쓸 회전 수. 이만큼 돌고 나면 최고 속도로 계속 돈다.")]
    [Min(1)]
    [SerializeField] private int _accelerationTurns = 5;

    private Tween _tween;

    private void OnEnable()
    {
        Play();
    }

    private void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
    }

    private void Play()
    {
        _tween?.Kill();

        // 시계 방향이 -Z다(화면 기준). 반시계는 부호를 뒤집는다.
        float direction = _isCounterClockwise ? 1f : -1f;
        float turnDegrees = FULL_TURN_DEGREES * direction;

        if (!_acceleratesOverTime)
        {
            _tween = transform
                .DOLocalRotate(new Vector3(0f, 0f, turnDegrees), _secondsPerTurn, RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Incremental)
                .SetUpdate(true);

            return;
        }

        // 회전마다 조금씩 빨라지는 시퀀스를 쌓고, 마지막 속도로 무한 반복을 잇는다.
        // DOTween의 Ease만으로는 "가속하면서 무한히 도는" 것을 표현할 수 없어 구간을 나눈다.
        Sequence sequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < _accelerationTurns; i++)
        {
            // 첫 바퀴는 기본 속도, 마지막 바퀴는 _finalSpeedMultiplier배 빠르게.
            float progress = (float)i / _accelerationTurns;
            float speed = Mathf.Lerp(1f, _finalSpeedMultiplier, progress);

            sequence.Append(transform
                .DOLocalRotate(new Vector3(0f, 0f, turnDegrees), _secondsPerTurn / speed,
                    RotateMode.LocalAxisAdd)
                .SetEase(Ease.Linear));
        }

        sequence.Append(transform
            .DOLocalRotate(new Vector3(0f, 0f, turnDegrees), _secondsPerTurn / _finalSpeedMultiplier,
                RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental));

        _tween = sequence;
    }
}
