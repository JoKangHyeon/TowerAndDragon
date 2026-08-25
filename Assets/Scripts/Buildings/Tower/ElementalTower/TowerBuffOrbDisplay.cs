using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프를 받는 타워 머리 위에 구체를 호선으로 띄운다.
///
/// MonoBehaviour가 아니다 - 타워 프리팹이 12종이라 컴포넌트로 만들면 전부 손으로 붙여야 하고,
/// [RequireComponent]는 이미 저장된 프리팹에 소급 적용되지 않는다.
/// (<see cref="MonsterStatusVfx"/>와 같은 형태다. 그쪽 주석도 함께 참고.)
///
/// 연출 인스턴스를 타워의 자식으로 두지 않고 풀 밑에 둔 채 위치만 옮기는 것도 같은 이유다 -
/// 타워가 사라질 때 자식이 함께 파괴되면 풀이 반납받지 못한 인스턴스를 빌려준 채로 기억한다.
/// </summary>
public sealed class TowerBuffOrbDisplay
{
    // 호선 배치. 개수와 무관하게 타워 중심축 대칭이 되도록 x를 중심에서의 오프셋으로 잡는다.
    //   x_i = (i - (n-1)/2) * spacing
    //   y_i = BASE_HEIGHT - CURVATURE * x_i^2
    // (n-1)/2를 빼는 것이 대칭을 보장한다 - 홀수면 가운데에 하나, 짝수면 좌우 짝이 된다.
    // 발밑 기준 높이다(ResolveAnchor가 스프라이트 경계의 아래쪽을 앵커로 준다).
    // 타워 스프라이트가 1.2월드라 1.4면 머리 위 0.2에 뜬다 - 더 띄우면 어느 타워의
    // 구체인지 눈으로 잇기 어려워진다.
    private const float BASE_HEIGHT = 1.4f;
    private const float CURVATURE = 0.5f;

    // 간격은 지름에 비례한다.
    private const float SPACING_RATIO = 1.16f;
    private const float BASE_DIAMETER = 0.31f;
    private const float SPACING = BASE_DIAMETER * SPACING_RATIO;

    // 타워 스프라이트(Building 레이어)보다 앞에 그린다. Building 레이어에는 이미 그리드 기반
    // 뎁스 정렬이 있으므로(Building.SetDepthSortOrder) 타워의 순서를 물려받아야 한다 -
    // Overlay로 올리면 그 정렬을 잃어 뒤쪽 구체가 앞쪽 타워를 덮는다.
    private const int SORTING_ORDER_OFFSET = 1;

    // 이름 조회는 한 번이면 된다 - Tick이 매 프레임 도는 경로라 거기서 부르지 않는다.
    private static readonly int BUILDING_SORTING_LAYER_ID =
        SortingLayer.NameToID(Defines.BUILDING_SORTING_LAYER_NAME);

    // 대여한 구체와 그 렌더러를 함께 들고 있는다. 렌더러를 매 프레임 GetComponentsInChildren로
    // 찾으면 배열이 프레임마다 새로 할당된다 - 대여 시점에 한 번만 찾는다.
    private readonly struct Orb
    {
        public readonly Transform Transform;
        public readonly ParticleSystemRenderer[] Renderers;

        public Orb(Transform transform)
        {
            Transform = transform;
            Renderers = transform.GetComponentsInChildren<ParticleSystemRenderer>(true);
        }
    }

    private readonly Dictionary<TowerAuraDataSO, Orb> _activeByAura = new();
    private readonly List<TowerAuraDataSO> _sourceBuffer = new(4);
    private readonly List<TowerAuraDataSO> _staleBuffer = new(4);

    private Tower _owner;
    private SpriteRenderer _bodyRenderer;

    /// <summary>소유 타워를 물린다. 렌더러 탐색이 들어 있으므로 한 번만 부른다.</summary>
    public void Bind(Tower owner)
    {
        _owner = owner;

        if (owner == null)
        {
            _bodyRenderer = null;
            return;
        }

        // SpriteRenderer로 좁혀 찾는다 - Renderer로 두면 오버레이·하이라이트가 먼저 잡혀
        // 손으로 확인하지 않은 타워에서만 구체가 엉뚱한 자리에 뜬다.
        _bodyRenderer = owner.GetComponent<SpriteRenderer>();

        if (_bodyRenderer == null)
        {
            _bodyRenderer = owner.GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    /// <summary>
    /// 걸린 오라를 다시 읽어 구체를 맞춘다. 매 프레임 부르는 유일한 진입점이다.
    ///
    /// 수집과 배치를 한 번에 하는 이유는 <see cref="TowerAuraSystem.CollectRecipientAuras"/>가
    /// 그리드를 통째로 훑기 때문이다 - 수집과 추종을 나누면 타워마다 그 순회가 두 번씩 돈다.
    ///
    /// 이미 떠 있는 구체는 건드리지 않으므로 매 프레임 불러도 연출이 다시 재생되지 않는다.
    /// </summary>
    public void Tick(TowerAuraSystem auraSystem)
    {
        if (_owner == null || auraSystem == null)
        {
            ReleaseAll();
            return;
        }

        auraSystem.CollectRecipientAuras(_owner, _sourceBuffer, out _);

        // 아무것도 안 걸렸고 떠 있는 것도 없으면 여기서 끝낸다 -
        // 대부분의 타워가 대부분의 시간 동안 지나는 경로다.
        if (_sourceBuffer.Count == 0 && _activeByAura.Count == 0)
        {
            return;
        }

        Reconcile();
        Layout();
    }

    /// <summary>떠 있는 구체를 전부 반납한다. 타워가 사라질 때 반드시 불러야 한다.</summary>
    public void ReleaseAll()
    {
        foreach (Orb orb in _activeByAura.Values)
        {
            ProjectilePool.ReleasePersistent(orb.Transform);
        }

        _activeByAura.Clear();
    }

    // 없어진 오라를 걷고 새로 걸린 오라를 대여한다.
    private void Reconcile()
    {
        // 순회 중에는 딕셔너리를 고칠 수 없어 걷어낼 키를 따로 모은다.
        _staleBuffer.Clear();

        foreach (TowerAuraDataSO aura in _activeByAura.Keys)
        {
            if (!_sourceBuffer.Contains(aura))
            {
                _staleBuffer.Add(aura);
            }
        }

        foreach (TowerAuraDataSO aura in _staleBuffer)
        {
            ProjectilePool.ReleasePersistent(_activeByAura[aura].Transform);
            _activeByAura.Remove(aura);
        }

        foreach (TowerAuraDataSO aura in _sourceBuffer)
        {
            if (_activeByAura.ContainsKey(aura))
            {
                continue;
            }

            Transform acquired = ProjectilePool.AcquirePersistent(aura.RecipientOrbPrefab);

            if (acquired != null)
            {
                _activeByAura.Add(aura, new Orb(acquired));
            }
        }
    }

    // 떠 있는 구체를 호선 위에 놓는다.
    private void Layout()
    {
        int count = _activeByAura.Count;

        if (count == 0)
        {
            return;
        }

        Vector3 anchor = ResolveAnchor();
        int sortingOrder = _owner.DepthSortOrder + SORTING_ORDER_OFFSET;

        // _sourceBuffer의 순서를 따른다. CollectRecipientAuras가 GridMap.Buildings를 훑는
        // 순서를 그대로 쓰므로, 같은 배치에서는 자리가 프레임마다 바뀌지 않는다.
        int index = 0;

        foreach (TowerAuraDataSO aura in _sourceBuffer)
        {
            if (!_activeByAura.TryGetValue(aura, out Orb orb) || orb.Transform == null)
            {
                continue;
            }

            float x = (index - (count - 1) * 0.5f) * SPACING;
            float y = BASE_HEIGHT - CURVATURE * x * x;

            orb.Transform.position = new Vector3(anchor.x + x, anchor.y + y, anchor.z);
            orb.Transform.localScale = Vector3.one;

            ApplySortingOrder(orb, sortingOrder);

            index++;
        }
    }

    // 구체는 타워 발밑을 기준으로 얹는다. BASE_HEIGHT가 발밑에서 잰 높이이기 때문이다 -
    // 몸통 중앙을 기준으로 삼으면 스프라이트 크기가 다른 타워에서 높이가 갈린다.
    private Vector3 ResolveAnchor()
    {
        if (_bodyRenderer == null)
        {
            return _owner.transform.position;
        }

        Bounds bounds = _bodyRenderer.bounds;

        // z는 타워의 것을 그대로 쓴다 - 경계의 z는 스프라이트 두께라 기준이 되지 못한다.
        return new Vector3(bounds.center.x, bounds.min.y, _owner.transform.position.z);
    }

    private static void ApplySortingOrder(in Orb orb, int sortingOrder)
    {
        foreach (ParticleSystemRenderer renderer in orb.Renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.sortingLayerID = BUILDING_SORTING_LAYER_ID;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
