using UnityEngine;

/// <summary>
/// 상태 지속 연출을 몬스터의 어디에 놓을지. <see cref="MonsterStatusVfx"/>가 읽는다.
///
/// 이 값을 상태 애셋(<see cref="StatusEffectSO"/>)이 아니라 연출 프리팹에 두는 이유:
/// 어디에 놓여야 하는지는 상태의 성질이 아니라 <b>그림의 성질</b>이다. 발밑에 깔리는 결정과
/// 몸을 감싸는 불꽃은 같은 상태에 붙어도 놓일 자리가 다르고, 반대로 같은 그림이라면 출처가
/// 타워든 새끼용이든 같은 자리에 놓여야 한다.
///
/// 붙이지 않은 프리팹은 <see cref="AnchorMode.Body"/>로 동작한다 - 기존 배선을 그대로 둬도 된다.
/// </summary>
public sealed class StatusVfxAnchor : MonoBehaviour
{
    public enum AnchorMode
    {
        /// <summary>스프라이트 경계의 중심. 몸을 감싸는 연출용.</summary>
        Body,

        /// <summary>스프라이트 경계의 아래쪽 끝. 바닥에 깔리는 연출용.</summary>
        Feet,
    }

    [Tooltip("연출을 몬스터의 어디에 맞출지. 바닥에 깔리는 그림이면 Feet.")]
    [SerializeField] private AnchorMode _mode = AnchorMode.Body;

    public AnchorMode Mode => _mode;
}
