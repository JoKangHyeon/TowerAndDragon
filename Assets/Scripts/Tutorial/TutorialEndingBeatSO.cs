using System.Collections.Generic;
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

    // 연출을 붙인 컷이 이보다 짧게 머무르면 연출을 알아보기 어렵다. 경고 기준일 뿐 강제하지 않는다.
    private const float MIN_EFFECT_HOLD_SECONDS = 1f;

    [Tooltip("이 컷에 띄울 문구의 스트링테이블 키.")]
    [SerializeField] private string _messageLocKey;

    [Tooltip("이 컷의 일러스트. 비우면 텍스트만 나온다 - 아트가 나오기 전까지는 비워 둔다.")]
    [SerializeField] private Sprite _illustration;

    [Tooltip("일러스트가 한 바퀴 도는 데 걸리는 시간(초). 0이면 돌지 않는다. " +
             "음수를 넣으면 반대로 돈다 - 시간이 되돌아가는 연출에 쓴다.")]
    [SerializeField] private float _illustrationSpinSeconds;

    [Tooltip("이 컷 동안 띄울 연출 프리팹들(시계·파티클·애니메이션 등). 컷이 끝나면 패널이 모두 지운다. " +
             "목록인 이유는 한 컷에 연출을 겹쳐 쌓기 위해서다 - 배경 연출 위에 파티클을 얹는 식이다. " +
             "리스트 순서가 그리는 순서이므로 뒤에 있을수록 위에 온다. " +
             "이 SO는 무엇이 재생되는지 알지 않는다 - 프리팹이 스스로 재생한다.")]
    [SerializeField] private List<GameObject> _effectPrefabs = new();

    [Tooltip("다음 버튼을 눌러 넘긴다. 읽는 속도는 사람마다 달라 대사 컷에는 이 방식을 권한다.")]
    [SerializeField] private bool _waitForConfirm = true;

    [Tooltip("다음 버튼을 쓰지 않을 때만 의미가 있다 - 이 시간(초)이 지나면 자동으로 넘어간다.")]
    [Min(MIN_HOLD_SECONDS)]
    [SerializeField] private float _holdSeconds = DEFAULT_HOLD_SECONDS;

    public string MessageLocKey => _messageLocKey;
    public Sprite Illustration => _illustration;
    public float IllustrationSpinSeconds => _illustrationSpinSeconds;
    public IReadOnlyList<GameObject> EffectPrefabs => _effectPrefabs;
    public bool WaitForConfirm => _waitForConfirm;
    public float HoldSeconds => _holdSeconds;

    private bool HasEffect
    {
        get
        {
            if (_effectPrefabs == null)
            {
                return false;
            }

            foreach (GameObject prefab in _effectPrefabs)
            {
                if (prefab != null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void OnValidate()
    {
        // 그림만 보여주는 컷(모래시계 등)은 문구가 없는 것이 정상이다 - 셋 다 없을 때만 빈 컷이다.
        if (string.IsNullOrWhiteSpace(_messageLocKey) && _illustration == null && !HasEffect)
        {
            Debug.LogWarning($"[TutorialEndingBeatSO] {name}: 문구도 일러스트도 연출도 없어 빈 화면이 됩니다.", this);
        }

        // 확인 버튼 없이 넘어가는 컷은 연출이 다 돌기 전에 지나갈 수 있다 - 눈치채기 어려운 조합이라 알린다.
        if (!_waitForConfirm && HasEffect && _holdSeconds < MIN_EFFECT_HOLD_SECONDS)
        {
            Debug.LogWarning(
                $"[TutorialEndingBeatSO] {name}: 연출이 있는데 {_holdSeconds}초 뒤 넘어갑니다 - " +
                "연출이 채 보이기 전에 컷이 바뀔 수 있습니다.", this);
        }
    }
}
