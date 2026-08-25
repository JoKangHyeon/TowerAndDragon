using System;
using Coffee.UIExtensions;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 스킬트리 노드 1개의 시각 표현.
//
// 생김새는 두 단계로 나뉜다.
//  - ApplyLayout: 노드 종류로 정해지는 크기(노드 지름·테두리 두께·아이콘 지름·라벨 글자 크기).
//    빌드 때 한 번만 부른다. 라벨·뱃지의 '위치'는 프리팹에 잡아 둔 자리를 그대로 쓴다.
//  - Bind: 상태에 따라 매 갱신마다 바뀌는 것(라벨·뱃지·색·클릭 핸들러).
//
// 어미용 노드와 새끼용 노드는 프리팹이 아니라 DragonSkillNodeStyleTable의 규격으로 갈린다.
//
// 해금 순간의 연출은 ProgressionUnlockEffect가 맡는다 - 연구트리(UI_ResearchNode)와 같은
// 연출이어야 해서 수치와 조립을 그쪽 한곳에 모아 두었다.
public class UI_DragonSkillNode : MonoBehaviour
{
    [Header("Visual Parts")]
    [FormerlySerializedAs("_background")]
    [Tooltip("바깥 테두리. 어떤 상태에서도 속성 색을 유지한다.")]
    [SerializeField] private Image _ring;

    [Tooltip("테두리 안쪽 채움. 어미용은 속성 색으로 차고, 새끼용은 비어 있다.")]
    [SerializeField] private Image _fill;

    [Tooltip("새끼용 노드에만 켜지는 알 아이콘.")]
    [SerializeField] private Image _icon;

    [Tooltip("아직 해금하지 않은 노드에 켜지는 자물쇠. 해금(Completed)하면 연출과 함께 꺼진다. " +
        "크기는 노드 지름에 비례해 런타임에 정하므로 프리팹 값은 쓰이지 않는다.")]
    [SerializeField] private Image _lock;

    [Tooltip("해금 순간에 뿌리는 파티클(UIParticle). 자식 파티클까지 함께 재생하므로 루트만 꽂으면 된다. " +
        "평소에는 오브젝트를 꺼 둔다 - 켜 두면 노드 70개가 전부 UIParticle 갱신에 참여한다.")]
    [SerializeField] private UIParticle _unlockParticle;

    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private TextMeshProUGUI _badge;
    [SerializeField] private Button _button;

    public DragonSkillNodeData Node { get; private set; }

    // 직전 Bind에서의 상태. "잠김 → 해금" 전이에서만 연출을 재생하려고 들고 있는다.
    // 아직 한 번도 바인딩되지 않았으면 null이라, 창을 다시 열어 이미 해금된 노드를 처음
    // 그릴 때는 재생되지 않는다(전이가 아니라 초기 상태이므로).
    private ProgressionNodeState? _previousState;

    private Sequence _unlockTween;

    // 연출이 도는 동안에는 Bind가 색·스케일을 덮어쓰지 않는다 - 자원 생산 틱마다 Bind가
    // 다시 도는데(UI_DragonSkillWindow가 ResourceChanged를 듣는다), 그때 값을 다시 칠하면
    // 연출이 첫 프레임에 끊긴다.
    private bool IsUnlockTweenPlaying => _unlockTween != null && _unlockTween.IsActive();

    /// <summary>
    /// 노드 종류로 정해지는 크기를 적용한다. 라벨·뱃지의 위치는 건드리지 않는다 -
    /// 프리팹에 잡아 둔 자리(노드 아래)를 그대로 쓴다.
    /// </summary>
    public void ApplyLayout(DragonNodeKind kind)
    {
        DragonSkillNodeStyle style = DragonSkillNodeStyleTable.For(kind);
        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(style.Diameter, style.Diameter);

        if (_fill != null)
        {
            // Fill은 (0,0)~(1,1)로 늘어나 있어 offset만 주면 테두리 두께만큼 안으로 들어간다.
            RectTransform fillRect = _fill.rectTransform;
            fillRect.offsetMin = new Vector2(style.RingThickness, style.RingThickness);
            fillRect.offsetMax = new Vector2(-style.RingThickness, -style.RingThickness);
        }

        if (_icon != null)
        {
            float iconDiameter = style.Diameter * DragonSkillNodeStyleTable.ICON_DIAMETER_RATIO;
            _icon.rectTransform.sizeDelta = new Vector2(iconDiameter, iconDiameter);
        }

        if (_lock != null)
        {
            float lockDiameter = style.Diameter * DragonSkillNodeStyleTable.LOCK_DIAMETER_RATIO;
            _lock.rectTransform.sizeDelta = new Vector2(lockDiameter, lockDiameter);
        }

        // 라벨·뱃지의 anchoredPosition은 프리팹 값을 그대로 둔다 - 글자 크기만 종류별로 맞춘다.
        if (_label != null)
        {
            _label.fontSize = style.LabelFontSize;
        }
    }

    public void Bind(
        DragonSkillNodeData node,
        ProgressionNodeState state,
        Color attributeColor,
        Sprite centerIcon,
        string badgeText,
        Action<DragonSkillNodeData> onClick)
    {
        Node = node;
        DragonSkillNodeStyle style = DragonSkillNodeStyleTable.For(node.Kind);

        bool justUnlocked = IsUnlockTransition(state);

        if (_label != null)
        {
            _label.text = StringTable.GetString(node.NameLocKey);
            _label.color = DragonSkillNodePalette.LabelColor(state);
        }

        // 링은 연출이 색을 직접 몰고 있으므로, 도는 동안 건드리지 않는다.
        if (_ring != null && !IsUnlockTweenPlaying)
        {
            _ring.color = DragonSkillNodePalette.RingColor(state, attributeColor);
        }

        if (_fill != null)
        {
            _fill.color = DragonSkillNodePalette.FillColor(state, attributeColor, style.IsKin);
        }

        if (_icon != null)
        {
            bool hasIcon = centerIcon != null;
            _icon.gameObject.SetActive(hasIcon);
            _icon.sprite = centerIcon;
            _icon.color = DragonSkillNodePalette.IconColor(state);
        }

        if (_badge != null)
        {
            bool hasBadge = !string.IsNullOrEmpty(badgeText);
            _badge.gameObject.SetActive(hasBadge);
            _badge.text = badgeText;
        }

        // 해금 = Completed. 살 수 있는 상태(Available)여도 아직 산 게 아니므로 자물쇠는 켜 둔다.
        // 방금 해금된 경우에는 즉시 끄지 않는다 - 연출이 자물쇠를 터뜨리며 끈다.
        if (!justUnlocked && !IsUnlockTweenPlaying)
        {
            ProgressionUnlockEffect.ResetAll(
                BuildEffectTargets(attributeColor),
                lockVisible: state != ProgressionNodeState.Completed);
        }

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() =>
            {
                SoundManager.Play(SoundId.UiButtonClick);
                onClick?.Invoke(Node);
            });
        }

        if (justUnlocked)
        {
            PlayUnlockEffect(attributeColor);
        }

        _previousState = state;
    }

    // 자물쇠가 풀리는 그 순간인지. 이미 해금된 노드를 다시 그리는 경우(창 재오픈, 다른 노드
    // 구매로 인한 전체 갱신)에는 상태가 그대로 Completed라 전이가 아니다.
    private bool IsUnlockTransition(ProgressionNodeState state) =>
        state == ProgressionNodeState.Completed &&
        _previousState.HasValue &&
        _previousState.Value != ProgressionNodeState.Completed;

    // 자물쇠가 덜컹거리다 터지고, 링이 번쩍이고, 노드가 한 번 튄다.
    private void PlayUnlockEffect(Color attributeColor)
    {
        _unlockTween?.Kill();
        _unlockTween = ProgressionUnlockEffect.Play(BuildEffectTargets(attributeColor));
    }

    private ProgressionUnlockEffect.Targets BuildEffectTargets(Color attributeColor)
    {
        return new ProgressionUnlockEffect.Targets
        {
            Node = transform,
            Lock = _lock,
            Flash = _ring,

            // 도착 색은 Bind가 완료 상태에 칠하는 값과 같아야 한다.
            FlashColor = DragonSkillNodePalette.RingColor(
                ProgressionNodeState.Completed, attributeColor),
            Particle = _unlockParticle,
        };
    }
}
