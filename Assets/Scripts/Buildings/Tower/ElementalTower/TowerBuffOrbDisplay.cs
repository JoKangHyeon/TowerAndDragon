using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택한 오라 타워의 수혜자들 머리 위에 구체를 하나씩 띄운다.
///
/// MonoBehaviour가 아니다 - 타워 프리팹이 12종이라 컴포넌트로 만들면 전부 손으로 붙여야 하고,
/// [RequireComponent]는 이미 저장된 프리팹에 소급 적용되지 않는다.
/// (<see cref="MonsterStatusVfx"/>와 같은 형태다. 그쪽 주석도 함께 참고.)
///
/// 연출 인스턴스를 타워의 자식으로 두지 않고 풀 밑에 둔 채 위치만 옮기는 것도 같은 이유다 -
/// 타워가 사라질 때 자식이 함께 파괴되면 풀이 반납받지 못한 인스턴스를 빌려준 채로 기억한다.
///
/// 한 번에 오라 하나만 그리므로(선택은 하나다) 한 타워에 구체가 둘 이상 뜰 일이 없다 -
/// 상시 표시 시절의 호선 배치는 그래서 사라졌다.
/// </summary>
public sealed class TowerBuffOrbDisplay
{
    // 스프라이트 경계의 **위쪽**에서 잰 여유다. 머리를 기준으로 잡는 것이 핵심이다 -
    // 발밑에서 잰 고정 높이로 두면 스프라이트가 낮은 타워에서 여유가 그만큼 늘어난다.
    // 실측: 버프·속성 타워 1.200월드 / 일반 공격 타워(활·석궁·머스킷) 0.960월드 -
    // 발밑 기준 1.4는 같은 상수인데 여유가 0.200 대 0.440으로 갈렸고, 버프를 받는 쪽이
    // 주로 공격 타워라 가장 자주 보이는 곳이 두 배 이상 떠 있었다.
    // (측정 스크립트: CoplayScripts/MeasureOrbHeadroom.cs)
    private const float HEAD_CLEARANCE = 0.12f;

    // 몸통 스프라이트를 못 찾은 타워용 대체 높이. 타워 원점에서 잰다.
    // 실측한 12종 프리팹은 전부 SpriteRenderer를 가지고 있어 실제로는 지나지 않는 길이다 -
    // 발밑 기준이던 시절의 값을 그대로 둬서 이 경로의 동작만은 바꾸지 않는다.
    private const float FALLBACK_HEIGHT = 1.4f;

    // 타워 스프라이트(Building 레이어)보다 앞에 그린다. Building 레이어에는 이미 그리드 기반
    // 뎁스 정렬이 있으므로(Building.SetDepthSortOrder) 그 타워의 순서를 물려받아야 한다 -
    // Overlay로 올리면 그 정렬을 잃어 뒤쪽 구체가 앞쪽 타워를 덮는다.
    private const int SORTING_ORDER_OFFSET = 1;

    // 이름 조회는 한 번이면 된다 - Layout이 매 프레임 도는 경로라 거기서 매번 부르지 않는다.
    //
    // ⚠️ 정적 필드 초기화자로 두면 안 된다. Unity는 SortingLayer.NameToID를 MonoBehaviour
    // 생성자(와 인스턴스 필드 초기화자) 안에서 부르는 것을 막는데, 코디네이터가 이 클래스를
    // 필드 초기화자에서 new 하므로 그 순간 static ctor가 함께 돌아 예외가 난다. 그러면 타입이
    // 통째로 죽어(TypeInitializationException) 코디네이터의 _orbDisplay가 null로 남고,
    // 첫 LateUpdate의 ReleaseAll에서 NullReferenceException이 된다. 첫 배치 때 한 번 읽는다.
    private const int UNRESOLVED_SORTING_LAYER_ID = int.MinValue;

    private static int _buildingSortingLayerId = UNRESOLVED_SORTING_LAYER_ID;

    private static int BuildingSortingLayerId
    {
        get
        {
            if (_buildingSortingLayerId == UNRESOLVED_SORTING_LAYER_ID)
            {
                _buildingSortingLayerId =
                    SortingLayer.NameToID(Defines.BUILDING_SORTING_LAYER_NAME);
            }

            return _buildingSortingLayerId;
        }
    }

    // 대여한 구체와 함께 매 프레임 쓰는 참조를 미리 찾아 둔다. 렌더러를 프레임마다
    // GetComponentsInChildren으로 찾으면 배열이 그때마다 새로 할당되고, 수혜자 몸통 렌더러도
    // 타워당 한 번이면 되는 조회다 - 대여 시점에 한 번만 찾는다.
    private readonly struct Orb
    {
        public readonly Transform Transform;
        public readonly ParticleSystemRenderer[] Renderers;
        public readonly SpriteRenderer RecipientBody;

        public Orb(Transform transform, Tower recipient)
        {
            Transform = transform;
            Renderers = transform.GetComponentsInChildren<ParticleSystemRenderer>(true);
            RecipientBody = ResolveBodyRenderer(recipient);
        }
    }

    // 키는 수혜자 타워다. 상시 표시 시절에는 오라별이었다 - 그때는 한 타워가 여러 오라를
    // 동시에 받는 것을 그려야 했고, 지금은 오라 하나에 대한 수혜자 목록을 그린다.
    private readonly Dictionary<Tower, Orb> _activeByRecipient = new();
    private readonly List<Tower> _staleBuffer = new();

    // 어느 오라의 구체를 빌려 왔는지 기억한다. 선택을 다른 색 오라로 옮겼을 때 범위가 겹치는
    // 수혜자는 목록에 그대로 남으므로, 이 비교가 없으면 이전 색 구체가 그 타워에 계속 떠 있다.
    private GameObject _activePrefab;

    /// <summary>
    /// 수혜자 목록에 맞춰 구체를 맞춘다. 매 프레임 부르는 유일한 진입점이다.
    /// 이미 떠 있는 구체는 건드리지 않으므로 매 프레임 불러도 연출이 다시 재생되지 않는다.
    /// </summary>
    public void Sync(GameObject orbPrefab, List<Tower> recipients)
    {
        if (orbPrefab == null || recipients == null)
        {
            ReleaseAll();
            return;
        }

        if (_activePrefab != orbPrefab)
        {
            ReleaseAll();
            _activePrefab = orbPrefab;
        }

        // 아무도 안 받고 떠 있는 것도 없으면 여기서 끝낸다 -
        // 오라 타워를 고르지 않은 대부분의 프레임이 지나는 경로다.
        if (recipients.Count == 0 && _activeByRecipient.Count == 0)
        {
            return;
        }

        Reconcile(orbPrefab, recipients);
        Layout(recipients);
    }

    /// <summary>떠 있는 구체를 전부 반납한다. 선택이 풀릴 때 반드시 불러야 한다.</summary>
    public void ReleaseAll()
    {
        foreach (Orb orb in _activeByRecipient.Values)
        {
            if (orb.Transform != null)
            {
                ProjectilePool.ReleasePersistent(orb.Transform);
            }
        }

        _activeByRecipient.Clear();

        // 프리팹 기억도 함께 지운다 - 여기서 남겨 두면 "빌려온 것이 없는데 프리팹은 그것"이라는
        // 상태가 생겨, 같은 오라를 다시 고를 때 Sync의 != 비교가 새 대여를 알아채지 못한다.
        // (Sync가 프리팹 교체를 감지해 이 메서드를 부른 직후 곧바로 다시 세운다.)
        _activePrefab = null;
    }

    // 목록에서 빠진 타워의 구체를 걷고 새로 들어온 타워에 구체를 대여한다.
    private void Reconcile(GameObject orbPrefab, List<Tower> recipients)
    {
        // 순회 중에는 딕셔너리를 고칠 수 없어 걷어낼 키를 따로 모은다.
        _staleBuffer.Clear();

        foreach (KeyValuePair<Tower, Orb> pair in _activeByRecipient)
        {
            // 철거·파괴된 타워는 목록에 없지만 키로는 남는다 - 함께 걷는다.
            if (pair.Key == null || !recipients.Contains(pair.Key))
            {
                _staleBuffer.Add(pair.Key);
            }
        }

        foreach (Tower stale in _staleBuffer)
        {
            if (_activeByRecipient.TryGetValue(stale, out Orb orb) &&
                orb.Transform != null)
            {
                ProjectilePool.ReleasePersistent(orb.Transform);
            }

            _activeByRecipient.Remove(stale);
        }

        foreach (Tower recipient in recipients)
        {
            if (_activeByRecipient.ContainsKey(recipient))
            {
                continue;
            }

            Transform acquired = ProjectilePool.AcquirePersistent(orbPrefab);

            if (acquired != null)
            {
                _activeByRecipient.Add(recipient, new Orb(acquired, recipient));
            }
        }
    }

    // 각 구체를 그 타워의 중심축 위 한 점에 놓는다.
    private void Layout(List<Tower> recipients)
    {
        foreach (Tower recipient in recipients)
        {
            if (recipient == null ||
                !_activeByRecipient.TryGetValue(recipient, out Orb orb) ||
                orb.Transform == null)
            {
                continue;
            }

            orb.Transform.position = ResolveOrbPosition(recipient, orb.RecipientBody);
            orb.Transform.rotation = Quaternion.identity;

            // 크기는 프리팹에 구워져 있다(FX_BuffOrb_* 제작 시 지름 0.34월드로 맞췄다) -
            // 여기서 배수를 걸면 그 측정이 무의미해진다.
            orb.Transform.localScale = Vector3.one;

            // 정렬 순서는 소스가 아니라 그 수혜자의 것이다 - 소스 하나의 순서를 전부에 먹이면
            // 뒤쪽 타워의 구체가 앞쪽 타워를 덮는다.
            ApplySortingOrder(orb, recipient.DepthSortOrder + SORTING_ORDER_OFFSET);
        }
    }

    // 구체는 타워 머리 위에 얹는다. 스프라이트 경계의 위쪽에서 재므로 스프라이트 높이가
    // 다른 타워에서도 눈에 보이는 간격이 같다 (발밑·중앙 기준은 둘 다 갈린다).
    private static Vector3 ResolveOrbPosition(Tower recipient, SpriteRenderer body)
    {
        Vector3 origin = recipient.transform.position;

        if (body == null)
        {
            return new Vector3(origin.x, origin.y + FALLBACK_HEIGHT, origin.z);
        }

        Bounds bounds = body.bounds;

        // z는 타워의 것을 그대로 쓴다 - 경계의 z는 스프라이트 두께라 기준이 되지 못한다.
        return new Vector3(
            bounds.center.x,
            bounds.max.y + HEAD_CLEARANCE,
            origin.z);
    }

    // SpriteRenderer로 좁혀 찾는다 - Renderer로 두면 오버레이·하이라이트가 먼저 잡혀
    // 손으로 확인하지 않은 타워에서만 구체가 엉뚱한 자리에 뜬다.
    private static SpriteRenderer ResolveBodyRenderer(Tower recipient)
    {
        if (recipient == null)
        {
            return null;
        }

        SpriteRenderer body = recipient.GetComponent<SpriteRenderer>();

        return body != null
            ? body
            : recipient.GetComponentInChildren<SpriteRenderer>(true);
    }

    private static void ApplySortingOrder(in Orb orb, int sortingOrder)
    {
        foreach (ParticleSystemRenderer renderer in orb.Renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            renderer.sortingLayerID = BuildingSortingLayerId;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
