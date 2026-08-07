using UnityEngine;

/// <summary>
/// 이 오브젝트를 안내 대상으로 id에 등록한다. 팀원이 UI를 개편해도 앵커만 다시 붙이면 안내가 따라온다.
/// 활성일 때만 등록되므로, 닫힌 창 안의 앵커를 가리키는 단계는 그 창을 여는 단계보다 뒤에 와야 한다.
/// </summary>
public sealed class GuideAnchor : MonoBehaviour
{
    [Tooltip("안내가 이 오브젝트를 찾을 때 쓰는 id. 같은 id를 두 곳에 붙이면 나중에 켜진 쪽이 이긴다.")]
    [SerializeField] private GuideAnchorId _id = GuideAnchorId.None;

    private RectTransform _rect;

    public GuideAnchorId Id => _id;

    /// <summary>
    /// 런타임에 id를 정한다. <see cref="GuideAnchorBinder"/>가 팀원 프리팹을 수정하지 않고 앵커를 붙일 때 쓴다
    /// (AddComponent는 그 자리에서 Awake/OnEnable을 태우므로, 그 시점엔 id가 아직 None이다).
    /// </summary>
    public void Bind(GuideAnchorId id)
    {
        if (_id == id)
        {
            return;
        }

        bool isRegistered = isActiveAndEnabled && _rect != null && _id != GuideAnchorId.None;
        if (isRegistered)
        {
            GuideAnchorRegistry.Unregister(_id, _rect);
        }

        _id = id;

        if (isActiveAndEnabled && _rect != null && _id != GuideAnchorId.None)
        {
            GuideAnchorRegistry.Register(_id, _rect);
        }
    }

    private void Awake()
    {
        // 딤 구멍을 대상의 사각형으로 뚫으므로 RectTransform이 아니면 쓸 수 없다.
        _rect = transform as RectTransform;
        if (_rect == null)
        {
            Debug.LogWarning($"[GuideAnchor] {name}은 RectTransform이 아니라 안내 대상이 될 수 없습니다. UI 오브젝트에 붙여 주세요.", this);
        }
    }

    // id가 None인 것은 배선을 빠뜨린 경우도 있지만, Bind를 기다리는 중일 수도 있다(GuideAnchorBinder).
    // 어느 쪽이든 등록할 것이 없으므로 레지스트리까지 내려보내 경고를 내지 않는다
    // - 빠진 배선은 에디터 검증 창이 잡는다.
    private void OnEnable()
    {
        if (_rect != null && _id != GuideAnchorId.None)
        {
            GuideAnchorRegistry.Register(_id, _rect);
        }
    }

    private void OnDisable()
    {
        if (_rect != null && _id != GuideAnchorId.None)
        {
            GuideAnchorRegistry.Unregister(_id, _rect);
        }
    }
}
