using Coffee.UIExtensions;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 노드가 잠김에서 풀리는 순간의 연출. 용 스킬트리(<see cref="UI_DragonSkillNode"/>)와
/// 연구트리(<see cref="UI_ResearchNode"/>)가 공유한다.
///
/// 두 트리의 연출은 <b>같아야 한다</b>. 예전에는 같은 수치를 양쪽에 따로 두었는데,
/// "같아야 하는 값"을 두 벌 들고 있으면 한쪽만 손보고 넘어가기 십상이라 한곳으로 모았다.
///
/// 파티클이 아니라 DOTween UI 트윈이 뼈대인 이유: UI 캔버스가 Screen Space - Overlay라
/// ParticleSystem은 캔버스가 아니라 카메라가 그려서 UI 뒤에 깔리고, 크기 단위(1유닛=1픽셀)와
/// 마스크도 맞지 않는다. 파티클은 UIParticle로 감싼 것만 곁들인다.
/// </summary>
public static class ProgressionUnlockEffect
{
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

    // 테두리·슬롯이 흰색에서 제 색으로 돌아오며 한 번 번쩍인다.
    private const float FLASH_DURATION = 0.35f;

    // 파티클을 다 뿌리고 오브젝트를 되돌리기까지의 시간. 프리팹의 파티클 세 개 중 가장 긴 것이
    // 길이 0.5초 + 수명 0.45초라 1초면 덮는다. 파티클을 손보면 이 값도 같이 늘려야 한다 -
    // 짧으면 재생 중에 꺼져 파티클이 뚝 끊긴다.
    private const float PARTICLE_LIFETIME = 1f;

    private const float OPAQUE = 1f;

    /// <summary>
    /// 연출이 건드리는 것들. 노드마다 부품 이름이 달라(링 / 슬롯) 역할로만 받는다.
    /// </summary>
    public struct Targets
    {
        /// <summary>한 번 튀어오를 노드 루트.</summary>
        public Transform Node;

        /// <summary>덜컹거리다 터질 자물쇠. 없으면 그 구간을 건너뛰고 곧바로 열린 것으로 본다.</summary>
        public Image Lock;

        /// <summary>흰색에서 <see cref="FlashColor"/>로 번쩍일 그래픽(스킬트리는 링, 연구는 슬롯).</summary>
        public Graphic Flash;

        /// <summary>번쩍임의 도착 색. <b>바인딩이 완료 상태에 칠하는 값과 같아야 한다</b> -
        /// 다르면 연출이 끝난 순간 색이 한 번 튄다.</summary>
        public Color FlashColor;

        /// <summary>열리는 순간 뿌릴 파티클. 자식 파티클까지 함께 재생하므로 루트만 주면 된다.
        /// 색은 건드리지 않는다 - 두 트리가 같은 파티클을 같은 색으로 쓴다.</summary>
        public UIParticle Particle;
    }

    /// <summary>
    /// 연출을 조립해 돌린다. 트윈들은 같은 시퀀스에 시각(Insert)으로 꽂아
    /// 서로의 길이에 영향을 주지 않게 한다. 돌아온 시퀀스는 호출한 쪽이 들고 있다가
    /// 다음 연출 전에 Kill한다.
    /// </summary>
    public static Sequence Play(in Targets targets)
    {
        Sequence sequence = DOTween.Sequence().SetLink(targets.Node.gameObject);

        // 자물쇠가 없는 노드에서는 흔들 대상이 없으니 곧바로 열린 것으로 본다.
        float openTime = 0f;

        if (targets.Lock != null)
        {
            Transform node = targets.Node;
            Image lockImage = targets.Lock;

            ResetLockVisual(node, lockImage, lockVisible: true);

            sequence.Insert(0f, lockImage.rectTransform.DOPunchRotation(
                LOCK_SHAKE_PUNCH, LOCK_SHAKE_DURATION, LOCK_SHAKE_VIBRATO, LOCK_SHAKE_ELASTICITY));

            openTime = LOCK_SHAKE_DURATION;

            sequence.Insert(openTime, lockImage.rectTransform
                .DOScale(LOCK_BURST_SCALE, LOCK_BURST_DURATION)
                .SetEase(Ease.OutQuad));
            sequence.Insert(openTime, lockImage.DOFade(0f, LOCK_BURST_DURATION).SetEase(Ease.InQuad));

            // 스케일·알파·회전을 되돌려 두지 않으면 세이브를 다시 불러 같은 뷰를 재사용할 때
            // 자물쇠가 투명하고 기울어진 채로 켜진다. 파티클은 아직 뿌리는 중이라 건드리지 않는다.
            sequence.InsertCallback(openTime + LOCK_BURST_DURATION,
                () => ResetLockVisual(node, lockImage, lockVisible: false));
        }

        targets.Node.localScale = Vector3.one;
        sequence.Insert(openTime, targets.Node
            .DOScale(NODE_PUNCH_SCALE, NODE_PUNCH_UP_DURATION)
            .SetEase(Ease.OutQuad));
        sequence.Insert(openTime + NODE_PUNCH_UP_DURATION, targets.Node
            .DOScale(1f, NODE_PUNCH_DOWN_DURATION)
            .SetEase(Ease.InOutQuad));

        if (targets.Flash != null)
        {
            // 흰색은 From으로 줘서 흔들리는 동안이 아니라 열리는 순간부터 번쩍이게 한다
            // (색을 미리 대입하면 흔들림 내내 흰 노드가 보인다).
            sequence.Insert(openTime, targets.Flash
                .DOColor(targets.FlashColor, FLASH_DURATION)
                .From(Color.white)
                .SetEase(Ease.OutQuad));
        }

        if (targets.Particle != null)
        {
            UIParticle particle = targets.Particle;
            sequence.InsertCallback(openTime, () => PlayParticle(particle));
            sequence.InsertCallback(openTime + PARTICLE_LIFETIME, () => StopParticle(particle));
        }

        return sequence;
    }

    /// <summary>
    /// 연출이 건드리는 모든 값을 기본값으로 되돌린다. 시퀀스 중간이 아니라 "연출이 돌고 있지
    /// 않을 때"만 부르는 쪽이다 - 파티클까지 정리하므로 재생 중에 부르면 파티클이 잘린다.
    /// </summary>
    public static void ResetAll(in Targets targets, bool lockVisible)
    {
        ResetLockVisual(targets.Node, targets.Lock, lockVisible);

        // 연출이 중간에 끊겨 파티클이 켜진 채 남는 것을 막는다.
        if (targets.Particle != null && targets.Particle.gameObject.activeSelf)
        {
            StopParticle(targets.Particle);
        }
    }

    // 노드 스케일과 자물쇠(스케일·회전·알파·표시)만 되돌린다. 자물쇠가 터진 직후에도 부르므로
    // 파티클은 건드리지 않는다 - 파티클은 아직 뿌리는 중이다.
    private static void ResetLockVisual(Transform node, Image lockImage, bool lockVisible)
    {
        node.localScale = Vector3.one;

        if (lockImage == null)
        {
            return;
        }

        lockImage.rectTransform.localScale = Vector3.one;
        lockImage.rectTransform.localRotation = Quaternion.identity;

        Color lockColor = lockImage.color;
        lockColor.a = OPAQUE;
        lockImage.color = lockColor;

        lockImage.gameObject.SetActive(lockVisible);
    }

    // UIParticle은 오브젝트가 켜져 있어야 렌더링하므로, 재생 직전에 켜고 끝나면 다시 끈다.
    // (켜 둔 채로 두면 노드 수십 개가 전부 UIParticle 갱신에 참여한다)
    private static void PlayParticle(UIParticle particle)
    {
        particle.gameObject.SetActive(true);
        particle.Play();
    }

    private static void StopParticle(UIParticle particle)
    {
        particle.Stop();
        particle.gameObject.SetActive(false);
    }
}
