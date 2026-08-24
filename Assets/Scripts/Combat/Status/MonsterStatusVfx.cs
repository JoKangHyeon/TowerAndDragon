using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 1체에 걸린 상태의 지속 연출을 켜고 끈다. <see cref="MonsterStatusReceiver"/>가 소유하며,
/// MonoBehaviour가 아니다 - 몬스터 프리팹이 33종이라 컴포넌트로 만들면 전부 손으로 붙여야 하고,
/// [RequireComponent]는 이미 저장된 프리팹에 소급 적용되지 않는다.
/// (BaseMonster가 SpecialBehaviorRunner를 들고 있는 것과 같은 형태다.)
///
/// 연출 인스턴스를 몬스터의 자식으로 두지 않고 풀 밑에 둔 채 위치만 따라가게 하는 이유는 두 가지다.
/// - 몬스터 루트 스케일이 0.1~1.5로 갈려 있어, 자식으로 두면 맞춰 둔 연출 크기가 몬스터마다 달라진다.
/// - 사망이 Destroy(gameObject)라 자식 연출이 함께 파괴되고, 그러면 풀이 반납받지 못한 인스턴스를
///   빌려준 상태로 기억해 발동할 때마다 새로 Instantiate하게 된다.
/// </summary>
public sealed class MonsterStatusVfx
{
    // 스프라이트를 못 찾은 몬스터의 몸통 높이 대용값. 발밑에 붙어 보이는 것보다 낫다는 정도의
    // 최후 수단이며, 정상 경로는 항상 스프라이트 경계를 쓴다.
    private const float FALLBACK_BODY_HEIGHT = 0.5f;

    // 프리팹을 키로 쓴다 - 같은 화상이라도 StatusId는 출처별로 갈리지만(tower_fire_burn /
    // baby_fire_burn / dragon_fire_burn) 화면에 뜨는 불은 하나여야 한다. StatusId를 키로 잡으면
    // 출처가 둘일 때 같은 불이 두 겹으로 뜬다.
    private readonly Dictionary<GameObject, Transform> _activeByPrefab = new();

    // 프리팹별 위치 보정. 매 프레임 상태 목록을 다시 읽지 않도록 재조정 시점에만 갱신한다.
    private readonly Dictionary<GameObject, Vector2> _offsetByPrefab = new();

    // 프리팹별 앵커(StatusVfxAnchor). 위 보정과 같은 시점에 같은 키로 갱신한다 -
    // Follow가 인덱서로 읽으므로 _activeByPrefab에 있는 키는 여기에도 반드시 있어야 한다.
    private readonly Dictionary<GameObject, StatusVfxAnchor.AnchorMode> _anchorByPrefab = new();

    private readonly List<StatusEffectSO> _sourceBuffer = new(4);
    private readonly List<GameObject> _staleBuffer = new(4);

    private Transform _owner;

    // 몸통 위치의 기준. 몬스터마다 크기가 달라 고정 오프셋으로는 어느 한쪽이 반드시 어긋난다.
    private SpriteRenderer _bodyRenderer;

    /// <summary>소유 몬스터를 물린다. 렌더러 탐색이 들어 있으므로 Awake에서 한 번만 부른다.</summary>
    public void Bind(Transform owner)
    {
        _owner = owner;

        if (owner == null)
        {
            _bodyRenderer = null;
            return;
        }

        // 종류를 SpriteRenderer로 좁힌다 - Renderer로 두면 방어막·오버레이처럼 몸통이 아닌
        // 렌더러가 먼저 잡혀, 손으로 확인하지 않은 몬스터에서만 연출이 엉뚱한 자리에 뜬다.
        // 루트를 먼저 보는 것도 같은 이유다(몸통 스프라이트는 대개 루트에 있다).
        _bodyRenderer = owner.GetComponent<SpriteRenderer>();

        if (_bodyRenderer == null)
        {
            _bodyRenderer = owner.GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    /// <summary>
    /// 지금 걸린 상태에 맞춰 연출 인스턴스를 재조정한다. 이미 떠 있는 것은 건드리지 않으므로
    /// 같은 상태가 재부여돼도 연출이 처음부터 다시 재생되거나 겹쳐 생기지 않는다.
    /// </summary>
    public void Refresh(MonsterStatusReceiver receiver)
    {
        if (receiver == null)
        {
            return;
        }

        receiver.CollectActiveVfxSources(_sourceBuffer);

        // 없어진 상태를 먼저 걷는다. 순회 중에는 딕셔너리를 고칠 수 없어 키를 따로 모은다.
        _staleBuffer.Clear();

        foreach (GameObject prefab in _activeByPrefab.Keys)
        {
            if (!ContainsPrefab(_sourceBuffer, prefab))
            {
                _staleBuffer.Add(prefab);
            }
        }

        foreach (GameObject prefab in _staleBuffer)
        {
            ProjectilePool.ReleasePersistent(_activeByPrefab[prefab]);
            _activeByPrefab.Remove(prefab);
            _offsetByPrefab.Remove(prefab);
            _anchorByPrefab.Remove(prefab);
        }

        foreach (StatusEffectSO source in _sourceBuffer)
        {
            GameObject prefab = source.ActiveVfxPrefab;

            // 보정값은 매번 덮어쓴다 - 같은 프리팹을 쓰는 상태가 둘이면 나중 것을 따르지만,
            // 같은 연출이면 보정도 같은 것이 정상이라 실제로 갈릴 일이 없다.
            _offsetByPrefab[prefab] = source.ActiveVfxOffset;

            // 앵커는 상태가 아니라 그림의 성질이라 프리팹에서 읽는다(StatusVfxAnchor 주석 참고).
            // 대여한 인스턴스가 아니라 프리팹 애셋에서 읽으므로 대여 성공 여부와 무관하다.
            _anchorByPrefab[prefab] = ResolveAnchorMode(prefab);

            if (_activeByPrefab.ContainsKey(prefab))
            {
                continue;
            }

            Transform effect = ProjectilePool.AcquirePersistent(prefab);

            if (effect != null)
            {
                _activeByPrefab.Add(prefab, effect);
            }
        }

        // 방금 켠 연출이 한 프레임 동안 풀 원점에 머무르지 않도록 그 자리에서 위치를 잡는다.
        Follow();
    }

    /// <summary>떠 있는 연출을 몬스터의 앵커 위치로 옮긴다. 매 프레임 부른다.</summary>
    public void Follow()
    {
        // 대부분의 몬스터는 대부분의 시간 동안 상태 연출이 없다 - 그 경우 아무 일도 하지 않는다.
        if (_activeByPrefab.Count == 0 || _owner == null)
        {
            return;
        }

        // 두 앵커를 한 번에 구한다 - 프리팹마다 고르는 것이 달라도 경계 계산은 한 번이면 된다.
        ResolveAnchors(out Vector3 body, out Vector3 feet);

        foreach (KeyValuePair<GameObject, Transform> pair in _activeByPrefab)
        {
            Transform effect = pair.Value;

            // 씬 언로드 등으로 밖에서 파괴됐을 수 있다. 장부 정리는 ReleaseAll이 맡는다.
            if (effect == null)
            {
                continue;
            }

            Vector3 anchor = _anchorByPrefab[pair.Key] == StatusVfxAnchor.AnchorMode.Feet ? feet : body;
            Vector2 offset = _offsetByPrefab[pair.Key];
            effect.position = new Vector3(anchor.x + offset.x, anchor.y + offset.y, anchor.z);
        }
    }

    /// <summary>떠 있는 연출을 전부 반납한다. 몬스터가 사라질 때 반드시 불러야 한다.</summary>
    public void ReleaseAll()
    {
        foreach (Transform effect in _activeByPrefab.Values)
        {
            ProjectilePool.ReleasePersistent(effect);
        }

        _activeByPrefab.Clear();
        _offsetByPrefab.Clear();
        _anchorByPrefab.Clear();
    }

    // 스프라이트 경계(월드 AABB)에서 몸통 중앙과 발밑을 구한다. 경계를 쓰므로 루트 스케일이
    // 0.1~1.5로 갈려도 같은 코드로 두 자리를 찾는다 - 고정 오프셋이면 어느 크기에서 반드시 어긋난다.
    private void ResolveAnchors(out Vector3 body, out Vector3 feet)
    {
        if (_bodyRenderer == null)
        {
            feet = _owner.position;
            body = feet + new Vector3(0f, FALLBACK_BODY_HEIGHT, 0f);
            return;
        }

        Bounds bounds = _bodyRenderer.bounds;

        // z는 몬스터의 것을 그대로 쓴다 - 경계의 z는 스프라이트 두께라 정렬 기준이 되지 못한다.
        float z = _owner.position.z;

        body = new Vector3(bounds.center.x, bounds.center.y, z);
        feet = new Vector3(bounds.center.x, bounds.min.y, z);
    }

    // 붙이지 않은 프리팹은 Body다 - 기존 연출의 자리를 바꾸지 않기 위해서다.
    private static StatusVfxAnchor.AnchorMode ResolveAnchorMode(GameObject prefab)
    {
        StatusVfxAnchor anchor = prefab.GetComponent<StatusVfxAnchor>();

        return anchor == null ? StatusVfxAnchor.AnchorMode.Body : anchor.Mode;
    }

    // List.Exists는 델리게이트를 만들고 캡처가 붙는다 - 매 재조정마다 도는 자리라 직접 돈다.
    private static bool ContainsPrefab(List<StatusEffectSO> sources, GameObject prefab)
    {
        foreach (StatusEffectSO source in sources)
        {
            if (source.ActiveVfxPrefab == prefab)
            {
                return true;
            }
        }

        return false;
    }
}
