using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 은신 오라 안의 타워를 반투명하게 만든다.
///
/// 버프 연출과 달리 **선택과 무관한 상시 표시다.** 그래서 선택 기반인
/// <see cref="TowerBuffOrbCoordinator"/>에 얹지 않고 따로 돈다 - 거기에 넣으면 조기 반환에 걸려
/// "오라 타워를 클릭한 동안에만 반투명" 이 되고, 선택을 풀 때마다 알파가 돌아온다.
/// 이 컴포넌트는 <c>_towerAuraSystem</c> 참조도 필요 없다(타워가 자기 것을 들고 있다).
///
/// 소스 자신의 은신을 알리는 것은 이제 이 표시뿐이다 - 수혜자 구체는 소스를 제외하므로
/// (설계 §3-5), 이걸 빼면 은신 타워 자신이 숨었는지 알 길이 없어진다.
///
/// 타워 프리팹에 붙이지 않고 여기서 모아 도는 이유는 프리팹이 12종이기 때문이다
/// (<see cref="TowerBuffOrbCoordinator"/>와 같은 판단).
/// </summary>
public sealed class TowerStealthFadeCoordinator : MonoBehaviour
{
    // 실제로 놓고 정한 값이다. 낮출수록 "숨어 있다"가 분명해지지만 눈으로 찾기 어려워진다.
    // 클릭 판정은 알파와 무관하다 - 건물 선택은 그리드 셀 기준이고 SpriteHitTest도
    // 텍스처 알파가 아니라 실루엣 폴리곤을 쓴다. 즉 여기서 막히는 것은 조작이 아니라 시야다.
    private const float STEALTH_ALPHA = 0.5f;

    private GridMap _gridMap;

    // 지금 흐려 둔 타워와 그 몸통 렌더러. 렌더러를 매 프레임 GetComponent로 찾지 않으려고
    // 함께 들고 있는다.
    private readonly Dictionary<Tower, SpriteRenderer> _fadedByTower = new();

    private readonly HashSet<Tower> _stealthedBuffer = new();
    private readonly List<Tower> _staleBuffer = new();

    private void OnEnable()
    {
        ResolveDependencies();
    }

    private void OnDisable()
    {
        RestoreAll();
    }

    private void OnDestroy()
    {
        RestoreAll();
    }

    // 다른 알파·색 라이터(Building.SetHighlighted 등)보다 나중에 써야 한다.
    private void LateUpdate()
    {
        ResolveDependencies();

        if (_gridMap == null || !HasStealthAuraTower())
        {
            RestoreAll();
            return;
        }

        Sweep();
    }

    private void ResolveDependencies()
    {
        _gridMap ??= Object.FindFirstObjectByType<GridMap>();
    }

    // 은신 오라를 가진 타워가 아예 안 지어졌으면 전수 스윕을 건너뛴다.
    //
    // 이 검사는 데이터만 본다 - TryGetActiveAura를 쓰면 인구·감전을 확인하려고
    // CanOperate -> SoulPopulation -> ResolveModifiers로 들어가, 아끼려던 비용을 그대로 낸다.
    // 은신 타워가 서 있는데 인구가 0인 경우는 스윕까지 가지만, 그건 흔한 상태가 아니다.
    private bool HasStealthAuraTower()
    {
        foreach (Building building in _gridMap.Buildings)
        {
            if (building is Tower tower &&
                tower.Data is ITowerAuraDataProvider provider &&
                provider.HasTowerAura &&
                provider.TowerAura.IsStealth)
            {
                return true;
            }
        }

        return false;
    }

    // 은신 여부는 Tower.TargetType을 그대로 읽는다. 오라 수집을 다시 짜지 않는 이유가
    // 중복 회피만은 아니다 - CollectAuraRecipients는 source == target을 건너뛰지만
    // 은신은 소스 자신도 대상이다(설계 §9-1). 재구성하면 은신 타워 자신이 조용히 빠지는데,
    // 그게 자기 은신을 알리는 유일한 신호다.
    //
    // 몬스터의 표적 판정과 같은 값을 쓰므로 "반투명한데 맞는다"가 구조적으로 생기지 않는다.
    private void Sweep()
    {
        _stealthedBuffer.Clear();

        foreach (Building building in _gridMap.Buildings)
        {
            if (building is not Tower tower ||
                tower.TargetType != MonsterTargetType.None)
            {
                continue;
            }

            _stealthedBuffer.Add(tower);
            Fade(tower);
        }

        RestoreFadedExcept(_stealthedBuffer);
    }

    private void Fade(Tower tower)
    {
        if (!_fadedByTower.TryGetValue(tower, out SpriteRenderer body))
        {
            body = ResolveBodyRenderer(tower);

            if (body == null)
            {
                return;
            }

            _fadedByTower.Add(tower, body);
        }

        WriteAlpha(body, STEALTH_ALPHA);
    }

    private void RestoreFadedExcept(HashSet<Tower> keep)
    {
        // 순회 중에는 딕셔너리를 고칠 수 없어 걷어낼 키를 따로 모은다.
        _staleBuffer.Clear();

        foreach (KeyValuePair<Tower, SpriteRenderer> pair in _fadedByTower)
        {
            // 철거·파괴된 타워는 keep에 없지만 키로는 남는다 - 함께 걷는다.
            if (pair.Key == null || !keep.Contains(pair.Key))
            {
                _staleBuffer.Add(pair.Key);
            }
        }

        foreach (Tower stale in _staleBuffer)
        {
            RestoreOne(stale);
        }
    }

    private void RestoreAll()
    {
        _staleBuffer.Clear();
        _staleBuffer.AddRange(_fadedByTower.Keys);

        foreach (Tower tower in _staleBuffer)
        {
            RestoreOne(tower);
        }

        _fadedByTower.Clear();
    }

    // 원래 알파를 캐시하지 않고 1로 돌린다. 타워 몸통 알파를 쓰는 다른 시스템이 없어
    // (전수 조사 확인: 부활·비활성은 애니메이터 포즈로 처리한다) 캐시가 만드는 타이밍 위험
    // - 다른 라이터가 잠깐 바꾼 값을 "원래 값"으로 굳히는 것 - 만 남는다.
    private void RestoreOne(Tower tower)
    {
        if (_fadedByTower.TryGetValue(tower, out SpriteRenderer body) &&
            body != null)
        {
            WriteAlpha(body, 1f);
        }

        _fadedByTower.Remove(tower);
    }

    // 알파만 덮어쓴다 - 하이라이트 등 다른 연출이 RGB를 바꿔도 충돌하지 않게
    // (SpriteHoverFade가 같은 이유로 같은 형태다).
    private static void WriteAlpha(SpriteRenderer body, float alpha)
    {
        Color color = body.color;

        if (Mathf.Approximately(color.a, alpha))
        {
            return;
        }

        color.a = alpha;
        body.color = color;
    }

    // SpriteRenderer로 좁혀 찾는다 - Renderer로 두면 오버레이·하이라이트가 먼저 잡힌다.
    // 실측: 타워 12종 모두 SpriteRenderer가 정확히 하나이고, 새끼용만 자식(Sprite)에 있다.
    // (같은 판단이 TowerBuffOrbDisplay에도 있다 - 한쪽을 고치면 다른 쪽도 본다.)
    private static SpriteRenderer ResolveBodyRenderer(Tower tower)
    {
        if (tower == null)
        {
            return null;
        }

        SpriteRenderer body = tower.GetComponent<SpriteRenderer>();

        return body != null
            ? body
            : tower.GetComponentInChildren<SpriteRenderer>(true);
    }
}
