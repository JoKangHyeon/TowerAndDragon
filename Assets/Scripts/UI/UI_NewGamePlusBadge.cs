using TMPro;
using UnityEngine;

/// <summary>
/// HUD 우상단의 새 게임 + 뱃지. 난이도 점수를 표시하고, 호버 시 켜진 뮤테이터를 단계까지 나열한다.
///
/// 제약이 걸린 사실이 화면에 없으면 플레이어는 밸런스가 이상하다고 느끼지 제약이라고 느끼지 않는다.
/// 그래서 이 표시는 부가 기능이 아니라 뮤테이터 기능의 일부다(설계서 §8).
///
/// 뮤테이터 서비스와 툴팁 표시기는 씬 오브젝트라 프리팹에 박을 수 없어
/// UI_IngameWindow가 Construct로 주입한다(UI_SlimeSummaryIndicator와 같은 이유).
/// </summary>
public class UI_NewGamePlusBadge : MonoBehaviour
{
    // 뱃지 문구. {0} = 난이도 점수 합. 값 예) "NG+ {0}"
    private const string BADGE_LOC_KEY = "hud_newgameplus_badge";

    [Tooltip("난이도 점수를 그릴 텍스트. 한글이 들어갈 수 있으므로 Maplestory SDF 폰트를 쓴다.")]
    [SerializeField] private TMP_Text _scoreText;

    [Tooltip("호버 시 활성 뮤테이터 목록을 내는 트리거. 표시기는 Construct로 주입받아 넘긴다.")]
    [SerializeField] private UI_TooltipTrigger _tooltipTrigger;

    private RunModifierService _runModifiers;

    /// <summary>뮤테이터 서비스와 툴팁 표시기를 주입한다. 뮤테이터가 없으면 스스로 꺼진다.</summary>
    public void Construct(RunModifierService runModifiers, UI_TooltipPresenter tooltipPresenter)
    {
        _runModifiers = runModifiers;

        if (_tooltipTrigger != null)
        {
            _tooltipTrigger.SetPresenter(tooltipPresenter);
        }

        Refresh();
    }

    // 구독은 OnEnable, 첫 발화는 Construct(호출부의 OnEnable) 이후 - CLAUDE.md 이벤트 규칙.
    private void OnEnable()
    {
        StringTable.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= Refresh;
    }

    /// <summary>표시 여부·점수·툴팁 문구를 현재 스냅샷으로 다시 그린다.</summary>
    private void Refresh()
    {
        // 표준 모드(뮤테이터 0개)와 서비스 미배선은 같게 다룬다 - 둘 다 "제약이 없다"는 뜻이다.
        if (_runModifiers == null || !_runModifiers.IsNewGamePlus)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (_scoreText != null)
        {
            _scoreText.text = string.Format(
                StringTable.GetString(BADGE_LOC_KEY), _runModifiers.DifficultyScore);
        }

        if (_tooltipTrigger != null)
        {
            _tooltipTrigger.SetContent(
                RunMutatorTooltipBuilder.Build(_runModifiers.ActiveSelections));
        }
    }
}
