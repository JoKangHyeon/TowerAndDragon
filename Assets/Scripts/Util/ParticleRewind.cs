using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 풀에서 꺼내 쓰는 파티클 오브젝트의 되감기·정리. 두 동작은 짝이며, 한쪽만 하면 잔상이 남는다.
///
/// <see cref="VillagerDispatchSystem"/>이 점령 완료·타워 배출 연출에서 같은 처리를 하고 있고,
/// 투사체 연출도 같은 것이 필요해 한 곳으로 모았다. 파티클을 풀링하는 쪽은 여기를 쓴다.
/// </summary>
public static class ParticleRewind
{
    // 호출마다 배열을 새로 만들지 않으려고 돌려 쓰는 버퍼.
    //
    // GetComponentsInChildren의 배열 반환 오버로드는 호출마다 새 배열을 만든다. 발사 한 번에
    // 이 경로를 여러 번 지나므로(투사체 반납 + 머즐 꺼내기·반납 + 임팩트 꺼내기·반납) 그 할당이
    // 그대로 쌓이는데, 하필 ProjectilePool은 발사마다 할당하지 않으려고 만든 것이다.
    //
    // 정적 버퍼를 공유해도 되는 이유: 호출자는 전부 이 함수를 부르고 곧장 끝나며(ProjectilePool·
    // VillagerDispatchSystem), 아래 루프가 부르는 것은 엔진 API뿐이라 도로 이 함수로 들어오는
    // 경로가 없다. 유니티 메인 스레드 전용이기도 하다.
    private static readonly List<ParticleSystem> _particleBuffer = new();

    /// <summary>처음부터 다시 재생한다. 비활성화만으로는 재생 위치가 정해지지 않으므로(playOnAwake가
    /// 꺼져 있으면 멈춘 채로 다시 나타난다) 꺼낼 때마다 되감아야 한다.</summary>
    public static void PlayFromStart(Transform root)
    {
        if (root == null)
        {
            return;
        }

        // 채우기 전에 버퍼를 비우는 것은 이 API가 해 준다.
        root.GetComponentsInChildren(true, _particleBuffer);

        foreach (ParticleSystem particles in _particleBuffer)
        {
            // Clear를 빼면 월드 공간 파티클이 직전에 쓰던 자리에서 지금 자리까지 줄을 긋는다.
            particles.Clear(true);
            particles.Play(true);
        }

        ReleaseBuffer();
    }

    /// <summary>방출을 멈추고 남은 입자를 지운다. 지우지 않으면 다음에 꺼내 쓸 때
    /// 이전 자리의 잔상이 한 프레임 비친다.</summary>
    public static void StopAndClear(Transform root)
    {
        if (root == null)
        {
            return;
        }

        root.GetComponentsInChildren(true, _particleBuffer);

        foreach (ParticleSystem particles in _particleBuffer)
        {
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        ReleaseBuffer();
    }

    // 쓰고 나면 비운다 - 그대로 두면 다음 호출까지 파괴된 파티클 오브젝트를 물고 있게 된다.
    // 용량은 유지되므로 다시 채울 때 할당이 생기지 않는다.
    private static void ReleaseBuffer()
    {
        _particleBuffer.Clear();
    }
}
