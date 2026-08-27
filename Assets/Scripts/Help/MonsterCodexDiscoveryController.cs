using UnityEngine;

/// <summary>
/// 낮 출현 예고 카드에 실제로 뜬 적을 도감에 등록한다. HelpDiscoveryController의 축소판이지만
/// 팝업을 띄우지 않는다 - "조우했다는 붉은 점만 쓰고 팝업은 출력하지 않는다"는 요구가 그대로
/// 무장(arming)·팝업 큐 전부를 불필요하게 만든다(HelpDiscoveryController._isArmed 주석 참고:
/// 그 무장은 세이브 복원이 쏘는 이벤트에서 팝업만 걸러내기 위한 것이었는데, 여기는 걸러낼 팝업이 없다).
///
/// PortalWavePreviewRenderer와 같은 오브젝트(Waves.prefab)에 붙는다 - 참조가 그 컴포넌트 하나와
/// 카탈로그 에셋 하나뿐이라 프리팹 안에서 배선이 닫힌다(UI_HelpUnviewedBadge가 Button_Help에
/// 붙는 것과 같은 이유).
///
/// Waves.prefab은 SampleScene과 Tutorial 양쪽에 있으므로, 도움말과 달리 튜토리얼 중에도 해금된다.
/// 이것은 의도한 차이다 - HelpDiscoveryController가 SampleScene 전용인 이유는 튜토리얼 중에 설명
/// 팝업이 뜨면 안 되기 때문인데, 여기는 팝업이 없어 그 제약이 없다.
/// </summary>
public sealed class MonsterCodexDiscoveryController : MonoBehaviour
{
    [Tooltip("해금 후보 전체 목록.")]
    [SerializeField] private MonsterCodexCatalogSO _catalog;

    [Tooltip("낮 예고 카드가 실제로 띄운 적 종류를 알려준다. 없으면 해금이 영영 일어나지 않는다.")]
    [WiringOptional]
    [SerializeField] private PortalWavePreviewRenderer _previewRenderer;

    // 구독은 Awake에서 한다(CLAUDE.md 이벤트 초기화 규칙).
    private void Awake()
    {
        if (!WiringGuard.Require(_catalog, nameof(_catalog), this))
        {
            return;
        }

        if (_previewRenderer != null)
        {
            _previewRenderer.MonsterPreviewShown.AddListener(HandleMonsterPreviewShown);
        }
    }

    private void OnDestroy()
    {
        if (_previewRenderer != null)
        {
            _previewRenderer.MonsterPreviewShown.RemoveListener(HandleMonsterPreviewShown);
        }
    }

    // 카탈로그에 실린 적만 해금한다 - 실리지 않은 적(디버그용 등)까지 열어 버리면 절대 도감에
    // 나타나지 않을 키가 플레이어 프로필에 쌓인다.
    //
    // 이미 해금된 적은 HelpProfile.TryUnlock이 그 자리에서 false를 돌려주고 저장도 하지 않으므로,
    // 안개가 걷힐 때마다(예: 정찰 확장) 반복 호출돼도 비용이 거의 없다.
    private void HandleMonsterPreviewShown(MonsterData data)
    {
        if (data == null || _catalog == null || !_catalog.TryGet(data, out _))
        {
            return;
        }

        HelpProfile.TryUnlock(data.NameLocKey);
    }
}
