using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GuideAnchor가 자기 RectTransform을 id로 등록해 두는 곳. 안내 시스템은 씬 참조를 하나도 들지 않고
/// id로만 대상을 찾는다 - HUD 버튼이 전부 UI_IngameWindow의 private [SerializeField]라 코드로는 가져올 수 없고,
/// 대상이 20개가 넘으면 인스펙터 배선으로는 씬이 날아갈 때 복구가 불가능하기 때문이다.
/// StringTable과 같은 이유로 static이다 - 씬마다 레지스트리를 다시 이어줄 필요가 없어야 한다.
///
/// <b>id 하나에 후보를 여러 개 담는다.</b> 한 칸만 두었을 때는 나중에 등록한 것이 앞의 것을 덮어썼고,
/// 그 덮어쓴 쪽이 꺼지면서 <see cref="Unregister"/>가 항목을 지우면 <b>아직 살아 있는 앞의 것이 있는데도
/// id가 비어</b> 안내가 문구만 뜨는 일이 있었다(인구 패널의 산출 행이 줄어들 때 실제로 그랬다 -
/// 행 프리팹에 같은 id가 붙어 있어 복제본마다 다시 등록되고, 행이 줄면 마지막 복제본이 항목째 지웠다).
/// 목록으로 두면 마지막 후보가 빠져도 그 앞의 후보가 자연스럽게 이어받는다.
/// </summary>
public static class GuideAnchorRegistry
{
    private static readonly Dictionary<GuideAnchorId, List<RectTransform>> _anchorsById = new();

    /// <summary>
    /// 앵커가 새로 등록됐음을 알린다. 닫힌 창 안의 앵커는 진입 시점에 없으므로,
    /// 안내가 이걸 듣고 창이 열리는 순간 강조를 붙인다 - 매 프레임 되묻지 않기 위한 것.
    /// </summary>
    public static event System.Action<GuideAnchorId> AnchorRegistered;

    public static void Register(GuideAnchorId id, RectTransform anchor)
    {
        if (id == GuideAnchorId.None || anchor == null)
        {
            Debug.LogWarning($"[GuideAnchorRegistry] id({id})가 None이거나 대상이 비어 등록하지 않습니다.", anchor);
            return;
        }

        if (!_anchorsById.TryGetValue(id, out List<RectTransform> anchors))
        {
            _anchorsById[id] = anchors = new List<RectTransform>();
        }

        // OnDisable을 타지 못하고 파괴된 항목이 남아있을 수 있다(씬 전환 등). 지금까지는 TryGet만 이것을
        // 걷어냈는데, 아래 경고가 먼저 그 항목의 name을 읽으면 MissingReferenceException이 GuideAnchor.OnEnable
        // 안에서 터져 등록도 알림도 없이 끊긴다 - 그러면 그 앵커는 조용히 사라지고 안내가 아무 데도 못 가리킨다.
        for (int i = anchors.Count - 1; i >= 0; i--)
        {
            if (anchors[i] == null)
            {
                anchors.RemoveAt(i);
            }
        }

        // 같은 대상이 두 번 들어오면(재활성화 등) 한 칸만 차지하게 한다.
        if (anchors.Contains(anchor))
        {
            AnchorRegistered?.Invoke(id);
            return;
        }

        // 목록으로 바뀌어 덮어써도 앞의 것을 잃지는 않지만, 여전히 알린다 -
        // 한 id를 두 곳이 들고 있으면 <b>어느 쪽이 강조될지 데이터로는 알 수 없다</b>.
        // 실제로 이 경고가 산출 행 앵커가 서로 겹쳐 있던 것을 드러냈다.
        if (anchors.Count > 0)
        {
            Debug.LogWarning(
                $"[GuideAnchorRegistry] id {id}가 이미 {anchors[anchors.Count - 1].name}에 등록돼 있습니다. " +
                $"{anchor.name}이(가) 우선하며, 이쪽이 꺼지면 앞의 것으로 되돌아갑니다.", anchor);
        }

        anchors.Add(anchor);
        AnchorRegistered?.Invoke(id);
    }

    public static void Unregister(GuideAnchorId id, RectTransform anchor)
    {
        if (!_anchorsById.TryGetValue(id, out List<RectTransform> anchors))
        {
            return;
        }

        anchors.Remove(anchor);

        if (anchors.Count == 0)
        {
            _anchorsById.Remove(id);
        }
    }

    /// <summary>
    /// 없으면 조용히 false를 준다 - 닫힌 창 안의 앵커는 "아직 없는" 것이 정상이라 경고가 오히려 노이즈다.
    /// 배선이 빠졌는지는 에디터에서 확인한다(TowerAndDragon/Guide/앵커 검증).
    ///
    /// 후보가 여럿이면 <b>가장 나중에 등록된 것</b>을 준다 - 한 칸만 두던 시절의 "나중 것이 이긴다"와 같은 결과라
    /// 기존 단계들의 동작이 바뀌지 않는다.
    /// </summary>
    public static bool TryGet(GuideAnchorId id, out RectTransform anchor)
    {
        anchor = null;

        if (!_anchorsById.TryGetValue(id, out List<RectTransform> anchors))
        {
            return false;
        }

        for (int i = anchors.Count - 1; i >= 0; i--)
        {
            RectTransform candidate = anchors[i];

            // OnDisable을 타지 못하고 파괴된 항목이 남아있을 수 있다(씬 전환 등).
            if (candidate == null)
            {
                anchors.RemoveAt(i);
                continue;
            }

            // 꺼진 후보는 목록에서 빼지 않고 건너뛰기만 한다 - 다시 켜지면 그대로 이어받는다.
            // 꺼진 것을 돌려주면 안내가 "대상은 있는데 보이지 않는" 상태가 되어 말풍선까지 사라진다
            // (UI_GuideOverlay.RefreshDrawn의 isUiTargetVisible 판정).
            // GuideAnchor.OnDisable이 스스로 해제하므로 보통은 여기까지 오지 않는 방어선이다.
            if (!candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            anchor = candidate;
            return true;
        }

        // 살아 있는 후보가 하나도 남지 않았을 때만 항목을 버린다 - 꺼져 있을 뿐인 후보까지
        // 지우면 창이 다시 열려도 되살아나지 못한다.
        if (anchors.Count == 0)
        {
            _anchorsById.Remove(id);
        }

        return false;
    }
}
