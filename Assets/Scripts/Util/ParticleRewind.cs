using UnityEngine;

/// <summary>
/// 풀에서 꺼내 쓰는 파티클 오브젝트의 되감기·정리. 두 동작은 짝이며, 한쪽만 하면 잔상이 남는다.
///
/// <see cref="VillagerDispatchSystem"/>이 점령 완료·타워 배출 연출에서 같은 처리를 하고 있고,
/// 투사체 연출도 같은 것이 필요해 한 곳으로 모았다. 파티클을 풀링하는 쪽은 여기를 쓴다.
/// </summary>
public static class ParticleRewind
{
    /// <summary>처음부터 다시 재생한다. 비활성화만으로는 재생 위치가 정해지지 않으므로(playOnAwake가
    /// 꺼져 있으면 멈춘 채로 다시 나타난다) 꺼낼 때마다 되감아야 한다.</summary>
    public static void PlayFromStart(Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            // Clear를 빼면 월드 공간 파티클이 직전에 쓰던 자리에서 지금 자리까지 줄을 긋는다.
            particles.Clear(true);
            particles.Play(true);
        }
    }

    /// <summary>방출을 멈추고 남은 입자를 지운다. 지우지 않으면 다음에 꺼내 쓸 때
    /// 이전 자리의 잔상이 한 프레임 비친다.</summary>
    public static void StopAndClear(Transform root)
    {
        if (root == null)
        {
            return;
        }

        foreach (ParticleSystem particles in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
