using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GuideAnchor가 자기 RectTransform을 id로 등록해 두는 곳. 안내 시스템은 씬 참조를 하나도 들지 않고
/// id로만 대상을 찾는다 - HUD 버튼이 전부 UI_IngameWindow의 private [SerializeField]라 코드로는 가져올 수 없고,
/// 대상이 20개가 넘으면 인스펙터 배선으로는 씬이 날아갈 때 복구가 불가능하기 때문이다.
/// StringTable과 같은 이유로 static이다 - 씬마다 레지스트리를 다시 이어줄 필요가 없어야 한다.
/// </summary>
public static class GuideAnchorRegistry
{
    private static readonly Dictionary<GuideAnchorId, RectTransform> _anchorsById = new();

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

        // 조용히 덮으면 어느 쪽이 강조될지 알 수 없으므로 살아있는 중복은 알린다.
        if (_anchorsById.TryGetValue(id, out RectTransform existing) && existing != null && existing != anchor)
        {
            Debug.LogWarning(
                $"[GuideAnchorRegistry] id {id}가 이미 {existing.name}에 등록돼 있어 {anchor.name}으로 덮어씁니다.", anchor);
        }

        _anchorsById[id] = anchor;
        AnchorRegistered?.Invoke(id);
    }

    public static void Unregister(GuideAnchorId id, RectTransform anchor)
    {
        // 중복 등록됐던 쪽이 꺼질 때 살아있는 항목을 지우지 않도록, 자기가 등록한 것일 때만 뺀다.
        if (_anchorsById.TryGetValue(id, out RectTransform registered) && registered == anchor)
        {
            _anchorsById.Remove(id);
        }
    }

    /// <summary>
    /// 없으면 조용히 false를 준다 - 닫힌 창 안의 앵커는 "아직 없는" 것이 정상이라 경고가 오히려 노이즈다.
    /// 배선이 빠졌는지는 에디터에서 확인한다(TowerAndDragon/Guide/앵커 검증).
    /// </summary>
    public static bool TryGet(GuideAnchorId id, out RectTransform anchor)
    {
        if (_anchorsById.TryGetValue(id, out anchor))
        {
            if (anchor != null)
            {
                return true;
            }

            // OnDisable을 타지 못하고 파괴된 항목이 남아있을 수 있다(씬 전환 등).
            _anchorsById.Remove(id);
        }

        anchor = null;
        return false;
    }
}
