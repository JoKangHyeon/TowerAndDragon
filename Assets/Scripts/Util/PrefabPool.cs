using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 프리팹별로 유휴 인스턴스를 모아 두는 획득/반납형 풀.
///
/// <see cref="ComponentPool{T}"/>와 용도가 다르다 - 그쪽은 "이번 갱신에서 앞쪽 N개만 쓰고 뒤는 끈다"는
/// 인덱스형이라 개별 반납이라는 개념이 없다(하이라이트 스프라이트·점령 테두리처럼 매 갱신 전체를
/// 다시 그리는 렌더러 묶음용). 이 풀은 개체마다 수명이 다른 연출 오브젝트를 위한 것이다.
///
/// 프리팹을 키로 나눠 담는 이유: 한 계열에 여러 겉모습 프리팹이 섞여 있어(주민 5종·병사 5종),
/// 통을 하나로 합치면 병사를 요청했는데 주민이 나온다.
/// </summary>
public sealed class PrefabPool<T> where T : Component
{
    private readonly Transform _parent;

    // 프리팹 → 쉬고 있는(비활성) 인스턴스들.
    private readonly Dictionary<T, Stack<T>> _idleByPrefab = new();

    // 인스턴스 → 어느 프리팹에서 나왔는지. 반납할 때 같은 통으로 돌려보내려면 역인덱스가 필요하다.
    private readonly Dictionary<T, T> _prefabByInstance = new();

    public PrefabPool(Transform parent)
    {
        _parent = parent;
    }

    /// <summary>유휴 인스턴스가 있으면 되살려 주고, 없으면 새로 만든다. 프리팹이 널이면 널을 준다.</summary>
    public T Acquire(T prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        Stack<T> idle = GetIdleStack(prefab);
        T instance = null;

        // 씬 언로드 등으로 밖에서 파괴된 인스턴스가 통에 남아 있을 수 있다 - 살아 있는 것이 나올 때까지 버린다.
        while (instance == null && idle.Count > 0)
        {
            instance = idle.Pop();
        }

        if (instance == null)
        {
            instance = Object.Instantiate(prefab, _parent);
        }

        _prefabByInstance[instance] = prefab;
        instance.gameObject.SetActive(true);

        return instance;
    }

    /// <summary>
    /// 유휴 인스턴스를 미리 만들어 통에 채운다. 로딩 화면이 덮여 있는 동안 불러 Instantiate 비용을
    /// 플레이 중이 아닌 그 구간으로 옮기는 용도다.
    ///
    /// 이미 통에 있는 만큼은 빼고 부족한 개수만 만든다 - 두 번 불려도 두 배로 늘지 않는다.
    /// </summary>
    public void Prewarm(T prefab, int count)
    {
        if (prefab == null)
        {
            return;
        }

        Stack<T> idle = GetIdleStack(prefab);

        while (idle.Count < count)
        {
            T instance = Object.Instantiate(prefab, _parent);

            // 만든 즉시 끈다 - 같은 프레임 안이라 Update가 한 번도 돌지 않는다.
            // (Instantiate 시점에 Awake는 이미 돌았고, 그 비용을 앞당기는 것이 이 함수의 목적이다.)
            instance.gameObject.SetActive(false);
            idle.Push(instance);
        }
    }

    /// <summary>다 쓴 인스턴스를 비활성화해 통에 돌려놓는다. 파괴된 인스턴스에는 부르면 안 된다(<see cref="Forget"/> 참고).</summary>
    public void Release(T instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.gameObject.SetActive(false);

        // 풀을 거치지 않고 만들어진 인스턴스는 어느 통으로 보낼지 알 수 없다 - 비활성화만 하고 흘려보낸다.
        if (!_prefabByInstance.TryGetValue(instance, out T prefab))
        {
            return;
        }

        _prefabByInstance.Remove(instance);
        GetIdleStack(prefab).Push(instance);
    }

    /// <summary>밖에서 파괴된 인스턴스의 기록만 지운다. 파괴된 오브젝트를 통에 넣으면 다음 획득이 널을 만난다.</summary>
    public void Forget(T instance)
    {
        if (instance != null)
        {
            _prefabByInstance.Remove(instance);
        }
    }

    private Stack<T> GetIdleStack(T prefab)
    {
        if (!_idleByPrefab.TryGetValue(prefab, out Stack<T> idle))
        {
            idle = new Stack<T>();
            _idleByPrefab.Add(prefab, idle);
        }

        return idle;
    }
}
