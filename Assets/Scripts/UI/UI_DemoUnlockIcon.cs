using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정창 좌상단 용 아이콘. 연속 5회 클릭하면 "새 게임 +"를 강제로 해금한다(설계서 §7.4 확정안).
/// 심사·시연 자리에서 한 판을 끝까지 갈 시간이 없어 평가자가 이 모드를 아예 못 보는 문제를 막고,
/// 세이브를 날린 유저를 위한 치트성 숏컷 역할도 겸한다.
///
/// <b>UI_ConfigWindow가 아니라 아이콘 자신에게 붙는 자립 컴포넌트다.</b> 두 가지 이득이 있다.
///  1. 이미 700줄이 넘는 설정창에 필드와 상태를 더하지 않는다.
///  2. "창을 닫으면 카운터 리셋"이 공짜로 성립한다 - 창 루트가 SetActive(false)되면 활성 자식인
///     이 컴포넌트의 OnDisable이 돌기 때문이다. 창의 여닫는 경로가 여럿(Esc·닫기 버튼·바깥 클릭·
///     슬롯 창으로 전환)이라, Close()에 손으로 거는 방식은 한 경로만 빠뜨려도 카운터가 남는다.
///
/// 해금 사실을 화면에 반영하는 일은 이 컴포넌트가 하지 않는다 - <see cref="MetaProgress.Changed"/>가
/// 발화하고 <see cref="UI_TitleWindow"/>가 스스로 다시 그린다. 그래서 이 스크립트는 타이틀 화면을
/// 알지 않으며, 인게임 설정창에서 눌러도(구독자가 없어) 아무 에러 없이 기록만 남는다.
/// </summary>
public sealed class UI_DemoUnlockIcon : MonoBehaviour
{
    private const int REQUIRED_CLICK_COUNT = 5;

    [Tooltip("아이콘 버튼 - 보통 이 오브젝트 자신의 Button.")]
    [SerializeField] private Button _iconButton;

    [Tooltip("해금됐다는 것을 알리는 라벨. 평소 비활성이고 해금 성공 순간에만 켜진다. " +
        "창을 닫으면 다시 감춘다.")]
    [WiringOptional]
    [SerializeField] private LocalizedText _unlockedLabel;

    private int _clickCount;

    private void Awake()
    {
        if (WiringGuard.Require(_iconButton, nameof(_iconButton), this))
        {
            _iconButton.onClick.AddListener(HandleClicked);
        }

        // 라벨 key를 인스펙터가 아니라 코드에서 넣는다 - 프리팹에만 있는 key는 상수와 조용히
        // 어긋난다(UI_TutorialPromptPanel.ApplyLabelKeys와 같은 처리).
        if (_unlockedLabel != null)
        {
            _unlockedLabel.SetKey(NewGamePlusLocKeys.DEMO_UNLOCK_DONE);
        }

        HideLabel();
    }

    private void OnDestroy()
    {
        if (_iconButton != null)
        {
            _iconButton.onClick.RemoveListener(HandleClicked);
        }
    }

    // 창을 닫으면 여기로 온다. 우발적으로 며칠에 걸쳐 쌓인 클릭이 해금을 일으키지 않게 한다.
    private void OnDisable()
    {
        _clickCount = 0;
        HideLabel();
    }

    private void HandleClicked()
    {
        _clickCount++;

        if (_clickCount < REQUIRED_CLICK_COUNT)
        {
            return;
        }

        _clickCount = 0;

        // 이미 해금돼 있었다면 아무 피드백도 내지 않는다 - 축하 연출이 또 나오면 방금 무슨 일이
        // 일어났는지 오히려 헷갈린다.
        if (!MetaProgress.UnlockForDemo())
        {
            return;
        }

        Debug.Log("[UI_DemoUnlockIcon] 시연용 강제 해금이 적용되었습니다 - 새 게임 +가 공개됩니다.", this);

        // 조용히 성공하면 시연 중에 눌렸는지 알 수 없다. 소리와 라벨 둘 다 낸다.
        SoundManager.Play(SoundId.ResearchComplete);

        if (_unlockedLabel != null)
        {
            _unlockedLabel.gameObject.SetActive(true);
        }
    }

    private void HideLabel()
    {
        if (_unlockedLabel != null)
        {
            _unlockedLabel.gameObject.SetActive(false);
        }
    }
}
