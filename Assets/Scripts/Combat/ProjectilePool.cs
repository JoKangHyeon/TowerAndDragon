using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 투사체와 그 명중 이펙트의 풀. 통 자체는 <see cref="PrefabPool{T}"/>가 맡고, 이 클래스는
/// 그것을 정적으로 열어 주는 역할만 한다.
///
/// 접근 방식이 다른 매니저(SerializeField 주입)와 다른 이유는 <see cref="SoundManager"/>와 같다:
/// 발사하는 쪽이 런타임에 Instantiate되는 프리팹(타워·새끼용·몬스터)이라 씬 오브젝트를 주입할 수 없다.
///
/// 다만 <b>이 풀은 씬에 미리 놓지 않는다</b> - 첫 사용 시점에 스스로 만들어진다.
/// SoundManager는 카탈로그·믹서 그룹을 인스펙터에서 받아야 해서 씬 오브젝트가 필요하지만, 여기는
/// 받을 것이 없다. 배선을 요구하지 않으면 배선을 빠뜨릴 수도 없고, "풀이 없는 씬"이라는 두 번째
/// 경로가 사라져 어느 씬에서든 같은 코드가 돈다.
///
/// 활성 씬에 만들므로 씬이 언로드되면 함께 사라진다(DontDestroyOnLoad를 쓰지 않는다) -
/// 남겨 두면 이전 씬의 투사체 인스턴스가 파괴된 타겟을 문 채로 다음 씬까지 따라온다.
/// </summary>
public sealed class ProjectilePool : MonoBehaviour
{
    private const string POOL_OBJECT_NAME = "ProjectilePool (auto)";

    private static ProjectilePool _current;

    private PrefabPool<Projectile> _projectilePool;
    private PrefabPool<Transform> _effectPool;

    // 발사할 때마다 새로 만들지 않도록 한 번만 만들어 두는 반납 창구.
    private Action<Projectile> _releaseHandler;

    // 플레이모드 재진입 시 Reload Domain이 꺼져 있으면 이전 세션의 참조가 정적 필드에 그대로 남는다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = null;
    }

    // Unity 연산자 비교라 파괴된 오브젝트도 널로 걸러진다 - 씬이 바뀌어 이전 풀이 사라졌으면 새로 만든다.
    private static ProjectilePool Current =>
        _current != null ? _current : CreateInstance();

    /// <summary>
    /// 투사체를 꺼내 놓는다. 반납 창구가 함께 주입되므로 투사체는 자기가 나온 곳으로만 돌아간다.
    /// 프리팹에 <see cref="Projectile"/>이 없으면 널을 준다.
    /// </summary>
    public static Projectile Spawn(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
        {
            return null;
        }

        return Current.AcquireProjectile(prefab, position);
    }

    /// <summary>
    /// 명중 이펙트처럼 스스로 걷혀야 하는 연출을 꺼내 놓는다. <paramref name="seconds"/>가 지나면 반납한다.
    ///
    /// 0 이하를 넘겨도 <b>반드시 반납된다</b>(다음 프레임에 걷힌다). 예전에는 그 경우 반납을 걸지 않아
    /// 인스턴스가 풀 밖으로 새어 나갔고, 발사할 때마다 활성 오브젝트가 끝없이 쌓였다 -
    /// "안 걷히는 연출"이 필요하면 이 함수가 아니라 직접 띄워야 한다.
    /// </summary>
    public static void PlayForSeconds(
        GameObject prefab, Vector3 position, Quaternion rotation, float seconds)
    {
        if (prefab == null)
        {
            return;
        }

        Current.AcquireEffect(prefab, position, rotation, seconds);
    }

    /// <summary>
    /// 근거리 사격의 시작점과 명중점을 잇는 트레이서를 잠깐 재생한다.
    ///
    /// 명중 이펙트와 달리 <b>배속을 무시한 실시간</b>으로 걷힌다 - 짧게 보이는 것을 보완하는
    /// 연출인데 3배속에서 스케일 시간을 쓰면 스스로 2프레임으로 줄어든다.
    /// <see cref="ProjectileTracerVisual"/>의 페이드도 같은 기준이다(한쪽만 바꾸면 어긋난다).
    /// </summary>
    public static void PlayTracerForSeconds(
        GameObject prefab,
        Vector3 startPosition,
        Vector3 endPosition,
        float seconds,
        float headWidthWorld,
        float intensity,
        Color coreColor,
        Color glowColor)
    {
        if (prefab == null)
        {
            return;
        }

        Current.AcquireTracerEffect(
            prefab,
            startPosition,
            endPosition,
            seconds,
            headWidthWorld,
            intensity,
            coreColor,
            glowColor);
    }

    /// <summary>
    /// 스스로 걷히지 않는 연출을 꺼내 놓는다. 몬스터에 걸린 상태(화상 등)처럼 <b>끝나는 시점을
    /// 미리 알 수 없는</b> 연출용이며, 반납은 부르는 쪽이 <see cref="ReleasePersistent"/>로 한다.
    ///
    /// <see cref="PlayForSeconds"/>와 통을 공유한다 - 새 통을 만들 이유가 없다. 통은 프리팹으로
    /// 나뉘므로(<see cref="PrefabPool{T}"/>) 상태 연출과 명중 이펙트가 섞이지 않는다.
    /// </summary>
    public static Transform AcquirePersistent(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        return Current.AcquirePersistentEffect(prefab);
    }

    /// <summary><see cref="AcquirePersistent"/>로 꺼낸 연출을 되돌린다.</summary>
    public static void ReleasePersistent(Transform effect)
    {
        // 널 검사를 Current보다 위에 둔다 - Current는 풀이 없으면 새로 만드는 프로퍼티라,
        // 씬 정리 중(OnDisable)에 반납이 들어오면 방금 사라진 풀 오브젝트를 헛되이 되살린다.
        if (effect == null || _current == null)
        {
            return;
        }

        _current.ReleasePersistentEffect(effect);
    }

    /// <summary>
    /// 유휴 인스턴스를 미리 만들어 둔다. 첫 발사에서 Instantiate 비용이 몰리는 것을 로딩 구간으로 옮기는
    /// 용도이며, 부르지 않아도 동작은 같다.
    /// </summary>
    public static void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null || count < 1)
        {
            return;
        }

        Current.PrewarmInternal(prefab, count);
    }

    private static ProjectilePool CreateInstance()
    {
        // 이름은 하이러키에서 이것이 무엇인지 알아보기 위한 것이다 - 코드가 이름으로 찾지는 않는다.
        var host = new GameObject(POOL_OBJECT_NAME);

        _current = host.AddComponent<ProjectilePool>();

        return _current;
    }

    private void Awake()
    {
        // 정적 진입점이 만들지 않고 누군가 씬에 직접 붙였을 수도 있다 - 그 경우도 여기서 자리를 잡는다.
        //
        // 다만 이미 자리를 잡은 풀이 있으면 가로채지 않고 물러난다. 가로채면 앞 풀이 대여한 투사체가
        // 이쪽으로 반납되는데, 이쪽 장부에는 없는 인스턴스라 비활성화만 되고 양쪽 장부에서 사라진다 -
        // 그 뒤로는 모든 발사가 풀을 못 쓰고 새로 Instantiate된다.
        // 이 클래스는 인스펙터에서 받는 것이 없으므로(클래스 주석 참고) 늦게 온 쪽을 버려도 잃는 설정이 없다.
        if (_current != null && _current != this)
        {
            Debug.LogWarning("[ProjectilePool] 이미 풀이 있어 이 컴포넌트를 제거합니다.", this);
            Destroy(this);
            return;
        }

        _current = this;

        _projectilePool = new PrefabPool<Projectile>(transform);
        _effectPool = new PrefabPool<Transform>(transform);
        _releaseHandler = ReleaseProjectile;
    }

    private void OnDestroy()
    {
        if (_current == this)
        {
            _current = null;
        }
    }

    private Projectile AcquireProjectile(GameObject prefab, Vector3 position)
    {
        Projectile source = prefab.GetComponent<Projectile>();

        if (source == null)
        {
            Debug.LogError("[ProjectilePool] 투사체 프리팹에 Projectile이 없습니다.", prefab);
            return null;
        }

        Projectile projectile = _projectilePool.Acquire(source);

        if (projectile == null)
        {
            return null;
        }

        projectile.transform.SetPositionAndRotation(position, Quaternion.identity);
        projectile.BindRelease(_releaseHandler);

        // 궤적 재생은 여기서 하지 않는다 - 무엇을 언제 보여줄지는 ProjectileVisual이 정하고,
        // 발사 시점을 아는 것도 그쪽(Projectile.Launch → OnLaunched)이다.

        return projectile;
    }

    private void ReleaseProjectile(Projectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        // 반납되는 것은 항상 깨끗해야 한다 - 명중 없이 돌아오는 경로(발사 실패)도 있어서
        // OnHit의 정리에만 맡길 수 없다. 파티클이 없는 프리팹에는 아무 일도 하지 않는다.
        ParticleRewind.StopAndClear(projectile.transform);
        _projectilePool.Release(projectile);
    }

    private void AcquireEffect(
        GameObject prefab, Vector3 position, Quaternion rotation, float seconds)
    {
        Transform effect = _effectPool.Acquire(prefab.transform);

        if (effect == null)
        {
            return;
        }

        effect.SetPositionAndRotation(position, rotation);
        ParticleRewind.PlayFromStart(effect);

        // 반납은 조건 없이 건다. 시간이 0 이하라 걸지 않았더니 그 인스턴스가 풀 장부에서 빠진 채
        // 활성으로 남아, 발사할 때마다 새로 만들어지고 영영 회수되지 않았다.
        ReleaseEffectAfterAsync(effect, seconds, this.GetCancellationTokenOnDestroy()).Forget();
    }

    private void AcquireTracerEffect(
        GameObject prefab,
        Vector3 startPosition,
        Vector3 endPosition,
        float seconds,
        float headWidthWorld,
        float intensity,
        Color coreColor,
        Color glowColor)
    {
        Transform effect = _effectPool.Acquire(prefab.transform);

        if (effect == null)
        {
            return;
        }

        ProjectileTracerVisual tracer = effect.GetComponent<ProjectileTracerVisual>();

        if (tracer == null)
        {
            Debug.LogError("[ProjectilePool] 트레이서 프리팹에 ProjectileTracerVisual이 없습니다.", prefab);
            _effectPool.Release(effect);
            return;
        }

        // LineRenderer가 월드 공간이라 루트 위치는 그림에 영향을 주지 않는다.
        effect.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        tracer.Play(
            startPosition,
            endPosition,
            seconds,
            headWidthWorld,
            intensity,
            coreColor,
            glowColor);

        ReleaseEffectAfterAsync(
            effect, seconds, this.GetCancellationTokenOnDestroy(), ignoreTimeScale: true).Forget();
    }

    // 시간 반납을 걸지 않는다는 점만 AcquireEffect와 다르다. 회전은 걸지 않는다 - 몬스터를
    // 따라다니는 연출이라 진행 방향 같은 기준이 없다.
    private Transform AcquirePersistentEffect(GameObject prefab)
    {
        Transform effect = _effectPool.Acquire(prefab.transform);

        if (effect == null)
        {
            return null;
        }

        effect.rotation = Quaternion.identity;
        ParticleRewind.PlayFromStart(effect);

        return effect;
    }

    private void ReleasePersistentEffect(Transform effect)
    {
        ParticleRewind.StopAndClear(effect);
        _effectPool.Release(effect);
    }

    // ignoreTimeScale은 트레이서만 켠다. 명중 이펙트는 게임 시간에 묶여 있어야 배속에서
    // 전투 속도와 함께 빨라진다 - 거기까지 실시간으로 만들면 3배속에서 잔상이 밀린다.
    private async UniTaskVoid ReleaseEffectAfterAsync(
        Transform effect,
        float delaySeconds,
        CancellationToken token,
        bool ignoreTimeScale = false)
    {
        // WaitForSeconds에 0 이하를 넘기면 프레임을 하나도 쉬지 않아, 방금 켠 이펙트를 같은 프레임에
        // 도로 끄게 된다(한 장도 그려지지 않는다). 그래서 최소 한 프레임은 보장한다.
        if (delaySeconds > 0f)
        {
            await UniTask.WaitForSeconds(
                delaySeconds, ignoreTimeScale, cancellationToken: token);
        }
        else
        {
            await UniTask.Yield(token);
        }

        if (effect == null)
        {
            return;
        }

        ParticleRewind.StopAndClear(effect);
        _effectPool.Release(effect);
    }

    private void PrewarmInternal(GameObject prefab, int count)
    {
        // 투사체인지 이펙트인지로 통이 갈린다 - 부르는 쪽이 구분하지 않아도 알아서 나뉜다.
        Projectile projectile = prefab.GetComponent<Projectile>();

        if (projectile != null)
        {
            _projectilePool.Prewarm(projectile, count);
            return;
        }

        _effectPool.Prewarm(prefab.transform, count);
    }
}
