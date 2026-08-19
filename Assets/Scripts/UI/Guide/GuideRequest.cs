using UnityEngine;

/// <summary>
/// 이 요청이 화면에 무엇을 하려는지. 안내가 없다는 것(<see cref="IGuideRequestProvider.TryGetRequest"/>가
/// false)과 "표시권은 쥐되 지금은 그리지 않는다"는 서로 다르다 - 앞은 우선순위가 낮은 안내에 화면을 넘기고,
/// 뒤는 넘기지 않는다.
/// </summary>
public enum GuideRequestPhase
{
    /// <summary>이 요청대로 그린다.</summary>
    Draw,

    /// <summary>
    /// 아직 그릴 준비가 안 됐다 - <b>앞 프레임의 그림을 그대로 둔다.</b> 가리킬 대상이 이번 프레임에
    /// 아직 없거나(창이 열리는 중) 챕터가 첫 컷을 잡기 전인 경우다. 여기서 화면을 걷으면 그 프레임이
    /// 통째로 비어 안내가 바뀔 때마다 번쩍인다.
    /// </summary>
    KeepLast,

    /// <summary>
    /// 화면은 걷되 표시권은 쥔다. 월드 대상이 창에 가려 그릴 수 없거나, 마지막 컷을 끝내고
    /// 다음 챕터에 넘기기까지 읽을 틈을 두는 동안이다. 그냥 넘기면 그 틈에 낮은 우선순위 안내가 끼어든다.
    /// </summary>
    Hide,
}

/// <summary>
/// 안내 한 컷의 요청. 제공자가 자기 상태에서 계산해 넘기고 <see cref="UI_GuideOverlay"/>는 이것만 보고
/// 그린다 - 오버레이는 여전히 도메인을 모른다.
///
/// 딤과 입력 차단은 여기 없다. 오버레이가 대상·확인 버튼 유무로 스스로 정한다
/// (<see cref="KeepsInputOpen"/>이 그 자동 판단을 되돌리는 유일한 통로다).
/// </summary>
public readonly struct GuideRequest
{
    public readonly GuideRequestPhase Phase;
    public readonly RectTransform Target;
    public readonly Renderer WorldTarget;
    public readonly string MessageLocKey;
    public readonly object[] Args;
    public readonly bool BlocksTargetInteraction;
    public readonly bool KeepsInputOpen;
    public readonly bool ShowsConfirmButton;
    public readonly GuideBubbleSlot BubbleSlot;

    /// <summary>
    /// 이 컷이 떠 있는 동안 밤으로 넘어가도 되는지. 기본은 false다 - 안내가 떠 있는데 밤이 시작되면
    /// 건설·인구 배치가 잠겨 시키던 일을 할 수 없게 되고 말풍선만 남는다(UI_GuideOverlay.CanEndDay).
    ///
    /// 켜는 것은 <b>밤 버튼을 누르라고 시키는 컷 하나뿐</b>이다. 그 컷에서까지 막으면 시킨 대로 눌러도
    /// 아무 일이 없다. 딤은 그대로라 밤 버튼 밖은 여전히 눌리지 않으므로 오조작으로 밤이 오지는 않는다.
    /// </summary>
    public readonly bool AllowsNightStart;

    private GuideRequest(GuideRequestPhase phase)
    {
        Phase = phase;
        Target = null;
        WorldTarget = null;
        MessageLocKey = null;
        Args = null;
        BlocksTargetInteraction = false;
        KeepsInputOpen = false;
        ShowsConfirmButton = false;
        BubbleSlot = GuideBubbleSlot.Default;
        AllowsNightStart = false;
    }

    private GuideRequest(
        RectTransform target,
        Renderer worldTarget,
        string messageLocKey,
        bool blocksTargetInteraction,
        bool keepsInputOpen,
        bool showsConfirmButton,
        GuideBubbleSlot bubbleSlot,
        object[] args,
        bool allowsNightStart)
    {
        Phase = GuideRequestPhase.Draw;
        Target = target;
        WorldTarget = worldTarget;
        MessageLocKey = messageLocKey;
        Args = args;
        BlocksTargetInteraction = blocksTargetInteraction;
        KeepsInputOpen = keepsInputOpen;
        ShowsConfirmButton = showsConfirmButton;
        BubbleSlot = bubbleSlot;
        AllowsNightStart = allowsNightStart;
    }

    /// <summary>앞 프레임의 그림을 그대로 두게 한다. <see cref="GuideRequestPhase.KeepLast"/> 참고.</summary>
    public static GuideRequest KeepLast => new GuideRequest(GuideRequestPhase.KeepLast);

    /// <summary>화면을 걷고 표시권만 쥔다. <see cref="GuideRequestPhase.Hide"/> 참고.</summary>
    public static GuideRequest Hidden => new GuideRequest(GuideRequestPhase.Hide);

    /// <summary>
    /// UI 대상을 가리키는(또는 아무것도 가리키지 않는) 컷. 인자를 두지 않는 문구는 args가 null이다.
    /// </summary>
    public static GuideRequest Draw(
        RectTransform target,
        string messageLocKey,
        bool blocksTargetInteraction,
        bool keepsInputOpen,
        bool showsConfirmButton,
        GuideBubbleSlot bubbleSlot,
        object[] args,
        bool allowsNightStart = false)
    {
        return new GuideRequest(
            target, null, messageLocKey, blocksTargetInteraction, keepsInputOpen,
            showsConfirmButton, bubbleSlot, args, allowsNightStart);
    }

    /// <summary>맵 위 오브젝트를 가리키는 컷. 렌더러의 월드 바운드를 오버레이가 화면 사각형으로 투영한다.</summary>
    public static GuideRequest DrawWorld(
        Renderer worldTarget,
        string messageLocKey,
        bool blocksTargetInteraction,
        bool keepsInputOpen,
        bool showsConfirmButton,
        GuideBubbleSlot bubbleSlot,
        object[] args,
        bool allowsNightStart = false)
    {
        return new GuideRequest(
            null, worldTarget, messageLocKey, blocksTargetInteraction, keepsInputOpen,
            showsConfirmButton, bubbleSlot, args, allowsNightStart);
    }

    /// <summary>
    /// 앞 프레임에 그린 것과 같은 요청인지. 오버레이는 매 프레임 요청을 다시 받으므로, 같으면 다시 그리지
    /// 않아야 한다 - 다시 그리면 말풍선 펄스가 매 프레임 처음부터 시작해 멈춘 것처럼 보이고
    /// 레이아웃도 매 프레임 강제로 다시 계산된다.
    /// </summary>
    public bool Matches(in GuideRequest other)
    {
        if (Phase != other.Phase ||
            !ReferenceEquals(Target, other.Target) ||
            !ReferenceEquals(WorldTarget, other.WorldTarget) ||
            MessageLocKey != other.MessageLocKey ||
            BlocksTargetInteraction != other.BlocksTargetInteraction ||
            KeepsInputOpen != other.KeepsInputOpen ||
            ShowsConfirmButton != other.ShowsConfirmButton ||
            BubbleSlot != other.BubbleSlot ||
            AllowsNightStart != other.AllowsNightStart)
        {
            return false;
        }

        return MatchesArgs(Args, other.Args);
    }

    // 진행률처럼 값이 바뀌는 인자는 배열 참조가 같아도 내용이 다를 수 있고, 반대로 내용이 같은데
    // 배열만 새로 만들어졌을 수도 있다. 그래서 내용으로 본다 - 길이가 둘뿐이라 비용은 무시할 수준이다.
    private static bool MatchesArgs(object[] left, object[] right)
    {
        bool isLeftEmpty = left == null || left.Length == 0;
        bool isRightEmpty = right == null || right.Length == 0;

        if (isLeftEmpty || isRightEmpty)
        {
            return isLeftEmpty && isRightEmpty;
        }

        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (!Equals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// 안내를 낼 수 있는 쪽. <see cref="UI_GuideOverlay"/>가 매 프레임 우선순위 순으로 물어
/// <b>처음 참을 돌려준 하나</b>를 그린다.
///
/// <b>TryGetRequest는 읽기만 한다.</b> 상태 전이(단계 전진·게이트 등록·이벤트 발화·구독)는 제공자 자신의
/// <c>Update</c>에서 해야 한다 - 오버레이의 해석 패스는 한 프레임에 여러 번 돌 수 있고(게이트 질의),
/// 거기서 상태가 바뀌면 "누가 그리는가"를 묻는 것만으로 게임이 전진한다.
/// </summary>
public interface IGuideRequestProvider
{
    /// <summary>클수록 화면을 먼저 차지한다. 값은 <see cref="GuidePriority"/>에서 고른다.</summary>
    int Priority { get; }

    /// <summary>
    /// 지금 낼 안내가 있는지. false면 <b>화면을 넘긴다</b> - 우선순위가 낮은 제공자가 그릴 수 있다.
    /// 넘기지 않고 비워두려면 <see cref="GuideRequest.Hidden"/>을 돌려준다.
    /// </summary>
    bool TryGetRequest(out GuideRequest request);

    /// <summary>지금 화면을 쥔 제공자에게만 간다 - 오버레이가 직접 부르므로 남의 클릭이 섞이지 않는다.</summary>
    void OnConfirmClicked();

    /// <summary>딤에 삼켜진 클릭이 있었다. 막은 사유를 낼 기회다.</summary>
    void OnBlockedClicked();
}
