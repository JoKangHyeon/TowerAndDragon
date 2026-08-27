using UnityEngine;

/// <summary>
/// 오라 타워의 버프 범위가 넓어질 때 효과음을 낸다. 시간·은신·영혼·강화 타워 프리팹에만 붙인다.
///
/// 활성/비활성이 아니라 <b>유효 반경</b>을 본다. 인구를 더 넣어 범위가 커질 때마다 소리가 나야 하는데,
/// 꺼진 상태에서 켜지는 것도 반경 0에서 커지는 경우라 같은 규칙으로 처리된다.
///
/// TowerAuraSystem에는 상태 전환 이벤트가 없어서 매 프레임 관측한다.
/// </summary>
public class TowerAuraSoundEmitter : MonoBehaviour
{
    // StaffingRatio 계산이 미세하게 흔들려도 매 프레임 울리지 않도록 이만큼 커졌을 때만 재생한다.
    private const float MIN_RADIUS_GROWTH = 0.01f;

    private Tower _tower;
    private float _lastRadius;

    private void Awake()
    {
        _tower = GetComponent<Tower>();
    }

    // Update가 아니라 LateUpdate인 이유: Tower.Start()/Setup()이 데이터를 채운 뒤에 관측해야 한다.
    private void LateUpdate()
    {
        if (_tower == null)
        {
            return;
        }

        float radius = TowerAuraSystem.TryGetActiveAura(_tower, out _, out float effectiveRadius)
            ? effectiveRadius
            : 0f;

        if (radius > _lastRadius + MIN_RADIUS_GROWTH)
        {
            PlayAuraOnSound();
        }

        _lastRadius = radius;
    }

    private void PlayAuraOnSound()
    {
        // 화면 밖이면 SoundManager가 걸러낸다. 재시도하지 않는다 - 인구 할당은 그 타워를 보면서 하는
        // 조작이라, 한참 전에 커진 범위가 카메라를 옮기는 순간 울리면 오히려 어긋난다.
        if (_tower.Data != null && _tower.Data.AuraOnSound is SoundId auraOnSound)
        {
            SoundManager.Play(auraOnSound, transform.position);
        }
    }
}
