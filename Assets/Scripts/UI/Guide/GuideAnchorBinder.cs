using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 팀원 프리팹을 수정하지 않고 안내 대상을 등록한다. 참조를 저장하는 쪽을 뒤집은 것이다 -
/// 앵커를 대상 프리팹 안에 넣는 대신, 튜토리얼 씬이 소유한 이 컴포넌트가 대상을 씬 참조로 들고 있다.
/// 그래서 대상 프리팹 인스턴스에는 오버라이드가 하나도 남지 않는다.
///
/// 왜 필요한가: master가 HUD(Ingame_window)를 개편하면서 거기 붙여 두었던 GuideAnchor 6개가 사라졌다.
/// 다시 붙이려면 팀원 프리팹을 고쳐야 하는데, 그러지 않기로 했다.
///
/// 대신 GuideAnchor를 런타임에 대상에 붙인다(직접 Register하지 않는다). 그래야
/// "활성일 때만 등록된다"는 기존 규칙이 그대로 성립한다 - 창이 닫히면 등록이 풀리고,
/// 다시 열리면 AnchorRegistered가 발화해 안내가 다시 조준한다. 직접 등록하면 닫힌 창을
/// 가리킨 채로 남아 딤 구멍이 엉뚱한 곳에 뚫린다.
/// 붙인 컴포넌트는 씬에 저장되지 않으므로(런타임 전용) 프리팹은 그대로다.
///
/// 프리팹 안에 살아 있는 앵커는 그대로 두고, 여기에는 대상이 사라진 id만 넣는다.
/// </summary>
public sealed class GuideAnchorBinder : MonoBehaviour
{
    [Serializable]
    private struct Binding
    {
        [Tooltip("등록할 안내 id.")]
        public GuideAnchorId Id;

        [Tooltip("그 id가 가리킬 대상. 이 씬의 UI 오브젝트를 그대로 참조한다.")]
        public RectTransform Target;
    }

    [Tooltip("id ↔ 대상 표. 프리팹에 앵커가 남아 있는 id는 넣지 않는다(중복 등록이 된다).")]
    [SerializeField] private List<Binding> _bindings = new();

    // 씬이 내려갈 때 내가 붙인 것만 정리하기 위해 들고 있는다.
    private readonly List<GuideAnchor> _attached = new();

    // 대상의 활성 상태를 따라가야 하므로 Start가 아니라 Awake에서 붙인다 -
    // 다른 오브젝트의 Start가 앵커를 조회하기 전에 등록이 끝나 있어야 한다.
    private void Awake()
    {
        foreach (Binding binding in _bindings)
        {
            Attach(binding);
        }
    }

    private void OnDestroy()
    {
        foreach (GuideAnchor anchor in _attached)
        {
            if (anchor != null)
            {
                Destroy(anchor);
            }
        }

        _attached.Clear();
    }

    private void Attach(Binding binding)
    {
        if (binding.Id == GuideAnchorId.None || binding.Target == null)
        {
            Debug.LogError($"[GuideAnchorBinder] id({binding.Id})가 None이거나 대상이 비어 있습니다.", this);
            return;
        }

        // 같은 대상에 id가 둘 붙는 것은 정상이다(한 버튼을 서로 다른 안내가 가리킬 수 있다).
        // 같은 id가 이미 그 대상에 붙어 있을 때만 건너뛴다 - 프리팹 앵커와 겹친 경우다.
        if (HasAnchorWithId(binding.Target, binding.Id))
        {
            Debug.LogWarning(
                $"[GuideAnchorBinder] {binding.Target.name}에 id {binding.Id} 앵커가 이미 있어 건너뜁니다.", this);
            return;
        }

        var anchor = binding.Target.gameObject.AddComponent<GuideAnchor>();
        anchor.Bind(binding.Id);
        _attached.Add(anchor);
    }

    private static bool HasAnchorWithId(RectTransform target, GuideAnchorId id)
    {
        GuideAnchor[] existing = target.GetComponents<GuideAnchor>();

        foreach (GuideAnchor anchor in existing)
        {
            if (anchor.Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
