using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자유 목표 목록을 상시 표시한다. 목표 상태는 TutorialObjectiveController가 들고 있고 이 창은 그리기만 한다 -
/// 나중에 다른 표시 수단을 붙일 때 컨트롤러를 고치지 않기 위해서다.
///
/// 1일차 강제 시퀀스 동안에는 보일 것이 없으므로 씬에 <b>비활성으로 저장</b>하고, 러너가 끝날 때
/// SetActive(true)로 켠다. Awake에서 자기 자신을 닫지 않으므로 CLAUDE.md의 _isOpen 가드는 필요 없다 -
/// 나중에 토글 버튼을 붙이게 되면 그때는 가드를 반드시 넣어야 한다.
///
/// 계층상 UI_GuideOverlay의 _overlayRoot보다 <b>뒤 형제</b>에 두어야 한다. 오버레이는 딤 패널을
/// 런타임에 _overlayRoot 아래로 만들고 대상이 없을 때 화면 전체를 덮으므로, 앞 형제에 있으면
/// 새끼용 안내가 뜨는 동안(하필 2일차) 목록이 딤에 가려진다.
/// </summary>
public sealed class UI_TutorialObjectiveWindow : MonoBehaviour
{
    [SerializeField] private TutorialObjectiveController _controller;

    [Tooltip("목표 한 줄 프리팹.")]
    [SerializeField] private UI_TutorialObjectiveSlot _slotPrefab;

    [Tooltip("생성된 줄이 들어갈 부모.")]
    [SerializeField] private Transform _slotContainer;

    [Tooltip("목록 배경. 보일 목표가 없을 때는 빈 상자만 남으므로 함께 숨긴다.")]
    [SerializeField] private Graphic _background;

    private ComponentPool<UI_TutorialObjectiveSlot> _slotPool;

    private void Awake()
    {
        _slotPool = new ComponentPool<UI_TutorialObjectiveSlot>(_slotPrefab, _slotContainer);
    }

    // 구독 직후 현재 값을 한 번 반영한다 - 이 창은 1일차가 끝난 뒤에야 켜지므로 그전에 완료된 목표가
    // 이미 있을 수 있고, 그러면 켜지는 순간의 ObjectivesChanged를 기다릴 것이 없어 빈 목록으로 열린다.
    private void OnEnable()
    {
        if (_controller != null)
        {
            _controller.ObjectivesChanged.AddListener(Refresh);
        }

        // 포맷 인자가 없는 문구지만 완료 색을 다시 칠해야 하므로 창이 직접 다시 그린다.
        StringTable.OnLanguageChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (_controller != null)
        {
            _controller.ObjectivesChanged.RemoveListener(Refresh);
        }

        StringTable.OnLanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        if (_controller == null || _slotPool == null)
        {
            return;
        }

        System.Collections.Generic.IReadOnlyList<TutorialObjectiveSO> objectives = _controller.VisibleObjectives;

        for (int i = 0; i < objectives.Count; i++)
        {
            TutorialObjectiveSO objective = objectives[i];
            UI_TutorialObjectiveSlot slot = _slotPool.Get(i);
            slot.Setup(objective.TitleLocKey, _controller.IsCompleted(objective));
        }

        _slotPool.DeactivateFrom(objectives.Count);

        // 1일차에는 아직 열린 목표가 없다 - 그때 배경만 남으면 빈 상자가 떠 있는 것으로 보인다.
        if (_background != null)
        {
            _background.enabled = objectives.Count > 0;
        }

        if (_slotContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }
}
