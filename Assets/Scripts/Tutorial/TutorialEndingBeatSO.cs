using UnityEngine;

/// <summary>
/// 엔딩 컷씬의 한 컷. 문구·일러스트·넘기는 방식만 데이터로 담는다.
/// 안내 단계(TutorialStepSO)와 같은 이유로 에셋으로 쪼갠다 - 컷 수와 문구는 플레이테스트마다 바뀐다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Tutorial/Ending Beat", fileName = "TE_Beat")]
public sealed class TutorialEndingBeatSO : ScriptableObject
{
    private const float DEFAULT_HOLD_SECONDS = 4f;

    // 0을 허용하면 확인 버튼을 쓰지 않는 컷이 눈에 보이지도 않고 지나간다.
    private const float MIN_HOLD_SECONDS = 0.5f;

    [Tooltip("이 컷에 띄울 문구의 스트링테이블 키.")]
    [SerializeField] private string _messageLocKey;

    [Tooltip("이 컷의 일러스트. 비우면 텍스트만 나온다 - 아트가 나오기 전까지는 비워 둔다.")]
    [SerializeField] private Sprite _illustration;

    [Tooltip("다음 버튼을 눌러 넘긴다. 읽는 속도는 사람마다 달라 대사 컷에는 이 방식을 권한다.")]
    [SerializeField] private bool _waitForConfirm = true;

    [Tooltip("다음 버튼을 쓰지 않을 때만 의미가 있다 - 이 시간(초)이 지나면 자동으로 넘어간다.")]
    [Min(MIN_HOLD_SECONDS)]
    [SerializeField] private float _holdSeconds = DEFAULT_HOLD_SECONDS;

    public string MessageLocKey => _messageLocKey;
    public Sprite Illustration => _illustration;
    public bool WaitForConfirm => _waitForConfirm;
    public float HoldSeconds => _holdSeconds;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(_messageLocKey))
        {
            Debug.LogWarning($"[TutorialEndingBeatSO] {name}: 문구 키(_messageLocKey)가 비어 있습니다.", this);
        }
    }
}
