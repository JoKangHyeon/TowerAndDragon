using UnityEngine;

/// <summary>
/// HUD 도움말 버튼(Button_Help) 우상단의 미확인 표시. 도감 목록에 보이는 항목 중 아직 펼쳐 보지
/// 않은 것이 하나라도 있으면 점을 켠다.
///
/// 판정 규칙을 스스로 들지 않고 HelpCatalogSO에 묻는다 - 창(UI_HelpWindow)과 다른 답을 내면
/// "점은 있는데 볼 게 없다"가 된다.
///
/// UI_IngameWindow에 필드를 더하지 않고 독립 컴포넌트로 두는 이유는 배선 위치다. 이 컴포넌트가
/// Button_Help에 붙으면 참조가 카탈로그 에셋 하나와 자기 자식 하나뿐이어서 Ingame_window
/// 프리팹 안에서 배선이 닫힌다(UI_Canvas 오버라이드도, 씬 수정도 필요 없다).
///
/// HelpDiscoveryController가 없는 튜토리얼 씬에서도 UnlockedFromStart 항목 몫으로 동작한다.
/// </summary>
public sealed class UI_HelpUnviewedBadge : MonoBehaviour
{
    [Tooltip("도감 항목 전체 목록. UI_HelpWindow와 반드시 같은 에셋을 지정한다.")]
    [SerializeField] private HelpCatalogSO _catalog;

    [Tooltip("버튼 우상단의 붉은 점(Dot_Unviewed).")]
    [SerializeField] private GameObject _dot;

    private void Awake()
    {
        WiringGuard.Require(_catalog, nameof(_catalog), this);
        WiringGuard.Require(_dot, nameof(_dot), this);
    }

    private void OnEnable()
    {
        HelpProfile.Changed += Render;         // 해금되면 미확인이 늘어난다
        HelpProfile.ViewedChanged += Render;   // 도감에서 펼쳐 보면 줄어든다

        // 두 이벤트는 변경 시에만 오므로 활성 시점의 현재 값을 한 번 직접 반영한다.
        // 이 덕에 HelpDiscoveryController.Start()의 해금이 먼저 오든 나중에 오든 결과가 같다.
        Render();
    }

    private void OnDisable()
    {
        HelpProfile.Changed -= Render;
        HelpProfile.ViewedChanged -= Render;
    }

    private void Render()
    {
        if (_dot == null || _catalog == null)
        {
            return;
        }

        _dot.SetActive(_catalog.HasUnviewedVisibleEntry());
    }
}
