using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자식 오브젝트를 한 장씩 켜서 돌리는 프레임 애니메이션. 로딩 화면의 스피너용이다.
///
/// Layer Lab의 Loading_Rotate_* 프리팹은 legacy Animation 컴포넌트로 같은 일을 하지만, 로딩 중에는
/// 쓸 수 없다. 그 애니메이션은 <b>시간</b>으로 진행하는데 씬 로딩 중에는 프레임이 거의 그려지지 않아
/// (측정: 3.8초에 16프레임, 그중 비동기 구간 1486ms에 2프레임) 그려질 때마다 위상이 통째로 건너뛴다.
/// 12장 스프라이트가 무작위로 튀는 화면이 되어 회전으로 읽히지 않는다.
///
/// 그래서 <b>렌더된 프레임당 한 칸씩만</b> 진행한다. 프레임이 넉넉할 때는 원래 속도로 돌고,
/// 부족할 때는 느려지기만 한다 - 밀린 칸을 몰아서 건너뛰지 않으므로 순서가 유지된다.
///
/// timeScale에 영향받지 않는다(unscaledDeltaTime). 정지 상태에서 로딩하는 인게임 경로로 확장할 때 필요하다.
/// </summary>
public sealed class UI_FrameSteppedSpinner : MonoBehaviour
{
    // Loading_rotate.anim이 12프레임 0.917초(≈13fps)다. 원본과 같은 속도를 기본값으로 둔다.
    private const float DEFAULT_FRAMES_PER_SECOND = 13f;

    [Tooltip("초당 넘길 프레임 수. 렌더 프레임이 부족하면 이 값보다 느려진다.")]
    [SerializeField] private float _framesPerSecond = DEFAULT_FRAMES_PER_SECOND;

    [Tooltip("이 CanvasGroup의 알파가 0이면 멈춘다. 비워 두면 부모에서 자동으로 찾는다.")]
    [WiringOptional]
    [SerializeField] private CanvasGroup _visibilityGroup;

    // 자식을 인스펙터로 12개 끌어다 넣게 하지 않는다 - Layer Lab 프리팹의 자식 순서가 곧 프레임 순서다.
    private readonly List<GameObject> _frames = new();

    private float _elapsedSinceStep;
    private int _currentIndex;

    private float FrameInterval => _framesPerSecond > 0f ? 1f / _framesPerSecond : 0f;

    private bool IsVisible => _visibilityGroup == null || _visibilityGroup.alpha > 0f;

    private void Awake()
    {
        // 원본 프리팹의 legacy Animation이 같은 자식들을 시간 기준으로 켜고 끈다 - 켜 둔 채로는 둘이 싸운다.
        if (TryGetComponent(out Animation legacyAnimation))
        {
            legacyAnimation.enabled = false;
        }

        // 로딩 오버레이는 SetActive로 여닫지 않고 알파로만 감추므로, 그 알파를 보지 않으면
        // 타이틀이 떠 있는 내내 보이지 않는 캔버스를 초당 13번 리빌드한다.
        if (_visibilityGroup == null)
        {
            _visibilityGroup = GetComponentInParent<CanvasGroup>(true);
        }

        CollectFrames();
    }

    private void OnEnable()
    {
        _elapsedSinceStep = 0f;
        _currentIndex = 0;
        ShowOnly(_currentIndex);
    }

    private void Update()
    {
        if (_frames.Count == 0 || FrameInterval <= 0f || !IsVisible)
        {
            return;
        }

        _elapsedSinceStep += Time.unscaledDeltaTime;

        if (_elapsedSinceStep < FrameInterval)
        {
            return;
        }

        // 밀린 시간을 빼지 않고 버린다. 남겨 두면 로딩이 끝나 프레임이 돌아오는 순간
        // 쌓인 빚만큼 몰아서 넘어가 스피너가 순간 빨라진다.
        _elapsedSinceStep = 0f;
        _currentIndex = (_currentIndex + 1) % _frames.Count;
        ShowOnly(_currentIndex);
    }

    private void CollectFrames()
    {
        _frames.Clear();

        foreach (Transform child in transform)
        {
            _frames.Add(child.gameObject);
        }

        WiringGuard.RequireNotEmpty(_frames, nameof(_frames), this);
    }

    private void ShowOnly(int index)
    {
        for (int i = 0; i < _frames.Count; i++)
        {
            _frames[i].SetActive(i == index);
        }
    }
}
