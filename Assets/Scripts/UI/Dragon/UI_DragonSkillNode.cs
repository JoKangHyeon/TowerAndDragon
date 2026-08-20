using System;
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
// 해금 순간의 연출은 파티클이 아니라 DOTween UI 트윈이다 - UI 캔버스가 Screen Space - Overlay라
// ParticleSystem은 캔버스가 아니라 카메라가 그려서 UI 뒤에 깔리고, 크기 단위(1유닛=1픽셀)와
// 마스크도 맞지 않는다. Image/RectTransform을 직접 트윈하면 그 세 문제가 전부 없다.
public class UI_DragonSkillNode : MonoBehaviour
{
    // --- 해금 연출 수치 ---
    private const float NODE_PUNCH_SCALE = 1.18f;
    private const float NODE_PUNCH_UP_DURATION = 0.12f;
    private const float NODE_PUNCH_DOWN_DURATION = 0.16f;

    // 자물쇠는 먼저 덜컹거린다 - 바로 터지면 "풀렸다"가 아니라 "사라졌다"로 읽힌다.
    // 흔들림이 끝나는 시각이 곧 '열리는' 순간이고, 나머지 연출은 전부 그 뒤에 붙는다.
    private const float LOCK_SHAKE_DURATION = 0.25f;
    private const float LOCK_SHAKE_STRENGTH_DEGREES = 50f;
    private const int LOCK_SHAKE_VIBRATO = 14;

    // 2D UI에서 눈에 보이는 회전은 Z뿐이다. DOShakeRotation은 방향을 무작위로 뽑아 X·Y까지
    // 흔들기 때문에 노드마다 세기와 리듬이 달라 보이고, 방향을 Z로 고정하려 해도 흔들 각도를
    // XY 평면에서 계산하는 구조라 강도가 0으로 죽는다. 그래서 결정적으로 도는 Punch를 쓴다.
    private static readonly Vector3 LOCK_SHAKE_PUNCH =
        new Vector3(0f, 0f, LOCK_SHAKE_STRENGTH_DEGREES);

    // 1이면 반대쪽으로도 같은 크기로 되돌아온다 - 덜컹거리는 느낌은 여기서 나온다.
    private const float LOCK_SHAKE_ELASTICITY = 1f;

    // 자물쇠는 커지며 사라진다 - 그냥 페이드만 하면 "풀렸다"는 느낌이 약하다.
    private const float LOCK_BURST_SCALE = 1.6f;
    private const float LOCK_BURST_DURATION = 0.28f;

    // 링이 흰색에서 속성 색으로 돌아오며 한 번 번쩍인다.
    private const float RING_FLASH_DURATION = 0.35f;

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
            ResetEffectState(lockVisible: state != ProgressionNodeState.Completed);
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

    // 자물쇠가 덜컹거리다 터지고, 링이 번쩍이고, 노드가 한 번 튄다. 트윈들은 같은 시퀀스에
    // 시각(Insert)으로 꽂아 서로의 길이에 영향을 주지 않게 한다.
    private void PlayUnlockEffect(Color attributeColor)
    {
        _unlockTween?.Kill();

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        Transform nodeTransform = transform;

        // 자물쇠가 없는 노드에서는 흔들 대상이 없으니 곧바로 열린 것으로 본다.
        float openTime = 0f;

        if (_lock != null)
        {
            ResetEffectState(lockVisible: true);

            sequence.Insert(0f, _lock.rectTransform.DOPunchRotation(
                LOCK_SHAKE_PUNCH, LOCK_SHAKE_DURATION, LOCK_SHAKE_VIBRATO, LOCK_SHAKE_ELASTICITY));

            openTime = LOCK_SHAKE_DURATION;

            sequence.Insert(openTime, _lock.rectTransform
                .DOScale(LOCK_BURST_SCALE, LOCK_BURST_DURATION)
                .SetEase(Ease.OutQuad));
            sequence.Insert(openTime, _lock.DOFade(0f, LOCK_BURST_DURATION).SetEase(Ease.InQuad));

            // 스케일·알파·회전을 되돌려 두지 않으면 세이브를 다시 불러 같은 뷰를 재사용할 때
            // 자물쇠가 투명하고 기울어진 채로 켜진다.
            sequence.InsertCallback(openTime + LOCK_BURST_DURATION,
                () => ResetEffectState(lockVisible: false));
        }

        nodeTransform.localScale = Vector3.one;
        sequence.Insert(openTime, nodeTransform
            .DOScale(NODE_PUNCH_SCALE, NODE_PUNCH_UP_DURATION)
            .SetEase(Ease.OutQuad));
        sequence.Insert(openTime + NODE_PUNCH_UP_DURATION, nodeTransform
            .DOScale(1f, NODE_PUNCH_DOWN_DURATION)
            .SetEase(Ease.InOutQuad));

        if (_ring != null)
        {
            // 도착 색은 Bind가 완료 상태에 칠하는 값과 같아야 한다 - 다르면 연출이 끝난 순간
            // 색이 한 번 튄다. 흰색은 From으로 줘서 흔들리는 동안이 아니라 열리는 순간부터
            // 번쩍이게 한다(색을 미리 대입하면 흔들림 내내 흰 링이 보인다).
            Color ringColor = DragonSkillNodePalette.RingColor(
                ProgressionNodeState.Completed, attributeColor);

            sequence.Insert(openTime, _ring
                .DOColor(ringColor, RING_FLASH_DURATION)
                .From(Color.white)
                .SetEase(Ease.OutQuad));
        }

        _unlockTween = sequence;
    }

    // 연출이 건드리는 모든 값(노드 스케일, 자물쇠 스케일·회전·알파·표시)을 기본값으로 되돌린다.
    private void ResetEffectState(bool lockVisible)
    {
        transform.localScale = Vector3.one;

        if (_lock == null)
        {
            return;
        }

        _lock.rectTransform.localScale = Vector3.one;
        _lock.rectTransform.localRotation = Quaternion.identity;

        Color lockColor = _lock.color;
        lockColor.a = 1f;
        _lock.color = lockColor;

        _lock.gameObject.SetActive(lockVisible);
    }
}
