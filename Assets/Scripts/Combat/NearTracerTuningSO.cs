using UnityEngine;

/// <summary>
/// 근거리 보조 트레이서의 튜닝값. 공격 투사체 타워 7종이 <b>하나의 애셋을 공유</b>한다.
///
/// v1은 이 값들을 7개 발사체 프리팹에 그대로 복제해 두어 한 값을 고치려면 7곳을 고쳐야 했다.
/// 프리팹이 갖는 것은 이 애셋 참조와 <b>타워별 글로우 색</b>뿐이다.
///
/// 값의 근거는 전부 실측이다 — `Docs/타워이펙트_작업노트.md` 5-3-1.
/// 눈대중으로 바꾸기 전에 그 절을 읽을 것. 특히 "왜 폭에는 강도를 걸지 않는가"(5-3-1 ③).
/// </summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Combat/Near Tracer Tuning",
    fileName = "NearTracerTuning")]
public sealed class NearTracerTuningSO : ScriptableObject
{
    private const float DEFAULT_REFERENCE_LENGTH_PIXELS = 280f;
    private const float DEFAULT_MIN_SCREEN_LENGTH_PIXELS = 64f;
    private const float DEFAULT_MAX_ORTHOGRAPHIC_SIZE = 10f;
    private const float DEFAULT_MIN_EFFECTIVE_INTENSITY = 0.2f;
    private const float DEFAULT_GLOW_HEAD_WIDTH_PIXELS = 8.6f;
    private const float DEFAULT_LIFETIME_SECONDS = 0.1f;
    private const float DEFAULT_MAX_EXTENSION_WORLD_UNITS = 1.2f;

    [Tooltip("화면 길이가 이 값 이상이면 트레이서를 붙이지 않는다. 짧을수록 강해진다.\n" +
             "280: 횡 최대 사격(432px)은 배제하고 상방 최대(199px)는 잡는 경계. 5-3-1 ④")]
    [Min(1f)]
    [SerializeField] private float _referenceLengthPixels = DEFAULT_REFERENCE_LENGTH_PIXELS;

    [Tooltip("이보다 짧으면 몬스터 쪽 끝을 고정하고 시작점을 뒤로 늘려 이 길이를 만든다.\n" +
             "64: 근거리 4케이스가 427~430픽셀로 수렴하는 값. 5-3-1 ②")]
    [Min(0f)]
    [SerializeField] private float _minScreenLengthPixels = DEFAULT_MIN_SCREEN_LENGTH_PIXELS;

    [Tooltip("이보다 축소하면 트레이서를 건너뛴다.\n" +
             "10: 이 위에서는 횡 최대 사격에도 강도가 붙기 시작하고 최소 길이가 " +
             "타워 스프라이트의 2~3.5배가 된다. 5-3-1 ⑥")]
    [Min(1f)]
    [SerializeField] private float _maxOrthographicSize = DEFAULT_MAX_ORTHOGRAPHIC_SIZE;

    [Tooltip("강도가 이보다 낮으면 건너뛴다. **성능 가드다 — 0으로 두지 말 것.**\n" +
             "기준 280px은 사거리 안 사격의 71%(Body)/66%(Ground)에 강도를 준다.\n" +
             "0.2면 52%/47%, 0.3이면 41%/38%로 내려간다. 5-3-1 MIN_EFFECTIVE_T 표")]
    [Range(0f, 1f)]
    [SerializeField] private float _minEffectiveIntensity = DEFAULT_MIN_EFFECTIVE_INTENSITY;

    [Tooltip("몬스터 쪽(굵고 밝은 끝) 글로우 폭을 화면 몇 px로 유지할지.\n" +
             "월드가 아니라 px다 — 줌에 따라 두께가 6배로 갈리던 v1 문제를 막는다. 5-1 ④")]
    [Min(0.5f)]
    [SerializeField] private float _glowHeadWidthPixels = DEFAULT_GLOW_HEAD_WIDTH_PIXELS;

    [Tooltip("페이드해 사라지기까지의 시간(초). 배속과 무관한 실시간이다.")]
    [Min(0f)]
    [SerializeField] private float _lifetimeSeconds = DEFAULT_LIFETIME_SECONDS;

    [Tooltip("시작점 연장의 월드 상한.\n" +
             "**허용 줌 범위 안에서는 물리지 않는다** - ortho 10에서 최소 길이가 1.185월드이고 " +
             "이 값이 1.2다. 줌 상한을 올렸을 때 타워보다 긴 줄이 생기는 것을 막는 예비 가드다.")]
    [Min(0f)]
    [SerializeField] private float _maxExtensionWorldUnits = DEFAULT_MAX_EXTENSION_WORLD_UNITS;

    public float MinScreenLengthPixels => _minScreenLengthPixels;
    public float MaxOrthographicSize => _maxOrthographicSize;
    public float MinEffectiveIntensity => _minEffectiveIntensity;
    public float GlowHeadWidthPixels => _glowHeadWidthPixels;
    public float LifetimeSeconds => _lifetimeSeconds;
    public float MaxExtensionWorldUnits => _maxExtensionWorldUnits;

    /// <summary>
    /// 화면 길이가 짧을수록 1에 가까운 강도. <b>알파에만 걸린다 — 폭에는 걸지 않는다.</b>
    ///
    /// 폭까지 줄이면 낮은 강도에서 선이 서브픽셀이 되어 통째로 사라진다
    /// (강도 0.10에서 폭 0.86px → 그려진 픽셀 0). 실측으로 뒤집힌 초안이다 — 5-3-1 ③.
    /// </summary>
    public float ResolveIntensity(float screenLengthPixels) =>
        1f - Mathf.Clamp01(screenLengthPixels / _referenceLengthPixels);

    private void OnValidate()
    {
        // 최소 길이가 기준을 넘으면 모든 사격이 최대 강도를 받아 램프가 무의미해진다.
        if (_minScreenLengthPixels >= _referenceLengthPixels)
        {
            Debug.LogWarning(
                $"[{nameof(NearTracerTuningSO)}] {name}: 최소 길이({_minScreenLengthPixels})가 " +
                $"기준 길이({_referenceLengthPixels}) 이상입니다. 램프가 동작하지 않습니다.", this);
        }

        // 가드가 꺼지면 "보조 연출"이 사거리 안 사격의 70%에 붙는다.
        if (_minEffectiveIntensity <= 0f)
        {
            Debug.LogWarning(
                $"[{nameof(NearTracerTuningSO)}] {name}: 최소 강도가 0입니다. " +
                "사거리 안 사격 대부분이 트레이서를 대여합니다(작업노트 5-3-1).", this);
        }
    }
}
