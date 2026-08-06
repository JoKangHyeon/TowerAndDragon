using System.Collections.Generic;

// Composite에 등록된 소스의 "소유권 개수"를 센다. 같은 소스를 여러 코디네이터가 각각
// Register/Unregister해도 마지막 소유자가 해제할 때까지 유지된다 - refcount 없이 List.Remove만
// 하면 한쪽의 해제가 다른 쪽의 기여까지 지워 버린다.
// 기여값은 등록 횟수와 무관하게 항상 1회만 계산되므로(중복 등록은 수명 관리일 뿐 가중치가 아님)
// 가산 Composite와 곱연산 Composite에 그대로 쓸 수 있다.
public sealed class RefCountedSourceSet<T> where T : class
{
    private readonly List<T> _sources = new();
    private readonly Dictionary<T, int> _refCountsBySource = new();

    /// <summary>등록 순서대로 중복 없이 담긴 소스 목록. 인덱스로 순회한다.</summary>
    public IReadOnlyList<T> Sources => _sources;

    public void Register(T source)
    {
        if (source == null)
        {
            return;
        }

        if (_refCountsBySource.TryGetValue(source, out int refCount))
        {
            _refCountsBySource[source] = refCount + 1;
            return;
        }

        _refCountsBySource.Add(source, 1);
        _sources.Add(source);
    }

    public void Unregister(T source)
    {
        if (source == null || !_refCountsBySource.TryGetValue(source, out int refCount))
        {
            return;
        }

        if (refCount > 1)
        {
            _refCountsBySource[source] = refCount - 1;
            return;
        }

        _refCountsBySource.Remove(source);
        _sources.Remove(source);
    }
}
