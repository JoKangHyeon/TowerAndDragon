using System.Collections.Generic;
using UnityEngine;

// 몬스터 1체에 걸린 지속 효과(슬로우·화상 등)를 보관·만료·틱 처리한다.
// BaseMonster에 [RequireComponent]로 강제돼 모든 몬스터 프리팹에 자동으로 붙는다 -
// 이 컴포넌트가 없으면 상태이상이 조용히 no-op이 되는 문제를 원천 차단한다.
//
// 중첩 규칙:
// - 이동속도: 동시에 여러 개가 걸려도 배율이 가장 작은(가장 강한) 것 하나만 적용한다.
//   곱으로 중첩하면 몇 번만 겹쳐도 0에 수렴해 밸런싱이 불가능해진다.
// - 지속피해: 같은 StatusId는 지속시간만 갱신(재적용), 다른 StatusId끼리는 병렬로 누적된다.
[RequireComponent(typeof(BaseMonster))]
public sealed class MonsterStatusReceiver : MonoBehaviour
{
    private struct MoveSpeedEntry
    {
        public MoveSpeedStatusSO Source;
        public bool IsInfinite;
        public float RemainingSeconds;
    }
    
    private struct FreezeEntry
    {
        public FreezeStatusSO Source;
        public bool IsInfinite;
        public float RemainingSeconds;
    }

    private struct DotEntry
    {
        public DamageOverTimeStatusSO Source;
        public DragonType? Element;
        public bool IsInfinite;
        public float RemainingSeconds;
        public float TickTimer;
    }
    // 다른 두 Entry와 달리 Source/Element를 보관하지 않는다 - 스택은 임계치에 도달하는 순간
    // 그 자리에서 곧바로 TriggerStatus를  부여하므로, 나중에 되읽을 값이 없다.
    private struct StackEntry
    {
        public bool IsInfinite;
        public float RemainingSeconds;
        public int Stacks;
    }

    private readonly Dictionary<string, MoveSpeedEntry> _moveSpeedStatuses = new();
    private readonly Dictionary<string, DotEntry> _dotStatuses = new();
    private readonly Dictionary<string, StackEntry> _stackStatuses = new();
    private readonly Dictionary<string, FreezeEntry> _freezeStatuses = new();
    private readonly List<string> _expiredKeys = new(4);

    public bool IsActionBlocked => _freezeStatuses.Count > 0;

    private BaseMonster _monster;

    // 활성 상태 중 가장 강한 이동속도 배율. 아무 것도 없으면 1(정상 속도).
    public float MoveSpeedMultiplier
    {
        get
        {
            float strongest = 1f;

            foreach (MoveSpeedEntry entry in _moveSpeedStatuses.Values)
            {
                strongest = Mathf.Min(
                    strongest,
                    entry.Source.SpeedMultiplier);
            }

            if (_freezeStatuses.Count > 0)
            {
                strongest = 0f;
            }

            return strongest;
        }
    }

    private void Awake()
    {
        _monster = GetComponent<BaseMonster>();
    }

    public void Apply(StatusEffectSO status)
    {
        Apply(status, null);
    }

    public void Apply(StatusEffectSO status, DragonType? element)
    {
        switch (status)
        {
            case null:
                return;
            case FreezeStatusSO freeze:
                ApplyFreeze(freeze);
                break;
            case MoveSpeedStatusSO moveSpeed:
                ApplyMoveSpeed(moveSpeed);
                break;
            case DamageOverTimeStatusSO dot:
                ApplyDot(dot, element);
                break;
            case StackingStatusEffectSO stacking:
                ApplyStack(stacking, element);
                break;
        }
    }

    public void Clear()
    {
        _moveSpeedStatuses.Clear();
        _dotStatuses.Clear();
        _stackStatuses.Clear();
        _freezeStatuses.Clear();

        _monster?.RefreshMoveSpeed();
    }

    private void ApplyMoveSpeed(MoveSpeedStatusSO status)
    {
        string key = ResolveKey(status);
        _moveSpeedStatuses[key] = new MoveSpeedEntry
        {
            Source = status,
            IsInfinite = status.IsInfinite,
            RemainingSeconds = status.DurationSeconds,
        };

        _monster?.RefreshMoveSpeed();
    }

    private void ApplyDot(DamageOverTimeStatusSO status, DragonType? element)
    {
        string key = ResolveKey(status);
        float tickTimer = _dotStatuses.TryGetValue(key, out DotEntry existing) ? existing.TickTimer : 0f;

        _dotStatuses[key] = new DotEntry
        {
            Source = status,
            Element = element,
            IsInfinite = status.IsInfinite,
            RemainingSeconds = status.DurationSeconds,
            TickTimer = tickTimer,
        };
    }

    private void ApplyStack (StackingStatusEffectSO status, DragonType? element)
    {
        string key = ResolveKey(status);
        int stacks = _stackStatuses.TryGetValue(key, out StackEntry existing)
            ? existing.Stacks + 1 
            : 1;
        
        if (stacks >= status.StacksToTrigger)
        {
            _stackStatuses.Remove(key);
            TriggerStack(status, element);
            return;
        }
        
        _stackStatuses[key] = new StackEntry
        {
            IsInfinite = status.IsInfinite,
            RemainingSeconds = status.DurationSeconds,
            Stacks = stacks,
        };
    }

    // 부여할 상태가 도 스택 상태이면 무시한다 - 서로를 가리키는 애셋 설정이 무한 재귀가 되는 것을 막는다.
    private void TriggerStack (StackingStatusEffectSO status, DragonType? element)
    {
        if (status.TriggeredStatus is null or StackingStatusEffectSO)
        {
            return;
        }
        Apply(status.TriggeredStatus, element);
    }

    private void ApplyFreeze(FreezeStatusSO status)
    {
        string key = ResolveKey(status);

        _freezeStatuses[key] = new FreezeEntry
        {
            Source = status,
            IsInfinite = status.IsInfinite,
            RemainingSeconds = status.DurationSeconds
        };

        _monster?.RefreshMoveSpeed();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        bool movementChanged = TickMoveSpeedStatuses(deltaTime);
        bool freezeChanged = TickFreezeStatuses(deltaTime);

        if (movementChanged || freezeChanged)
        {
            _monster?.RefreshMoveSpeed();
        }

        TickDotStatuses(deltaTime);
        TickStackStatuses(deltaTime);
    }

    private void TickStackStatuses (float deltaTime)
    {
        if (_stackStatuses.Count == 0)
        {
            return;
        }

        _expiredKeys.Clear();

        foreach (var kvp in _stackStatuses)
        {
            string key = kvp.Key;
            StackEntry entry = kvp.Value;

            if (entry.IsInfinite)
            {
                continue;
            }

            entry.RemainingSeconds -= deltaTime;

            // 유지 시간이 지나면 쌓인 스택을 통째로 비운다 (1개씩 감소가 아님)
            // 마지막 타격 이휴 5초안에 3번을 채워야 빙결
            if (entry.RemainingSeconds <= 0f)
            {
                _expiredKeys.Add(key);
            }
            else
            {
                _stackStatuses[key] = entry;
            }
        }

        foreach (var key in _expiredKeys)
        {
            _stackStatuses.Remove(key);
        }
    }

    // 딕셔너리의 값을 갱신해도 버전이 올라가지 않으므로 직접 순회 가능.
    // 만료된 키만 따로 모아서 삭제한다.
    private bool TickMoveSpeedStatuses(float deltaTime)
    {
        if (_moveSpeedStatuses.Count == 0)
        {
            return false;
        }

        _expiredKeys.Clear();
        bool anyExpired = false;

        foreach (var kvp in _moveSpeedStatuses)
        {
            string key = kvp.Key;
            MoveSpeedEntry entry = kvp.Value;

            if (entry.IsInfinite)
            {
                continue;
            }

            entry.RemainingSeconds -= deltaTime;

            if (entry.RemainingSeconds <= 0f)
            {
                _expiredKeys.Add(key);
                anyExpired = true;
            }
            else
            {
                _moveSpeedStatuses[key] = entry;
            }
        }

        foreach (var key in _expiredKeys)
        {
            _moveSpeedStatuses.Remove(key);
        }

        return anyExpired;
    }

    private bool TickFreezeStatuses(float deltaTime)
    {
        if (_freezeStatuses.Count == 0)
        {
            return false;
        }

        _expiredKeys.Clear();
        bool anyExpired = false;

        foreach (var kvp in _freezeStatuses)
        {
            string key = kvp.Key;
            FreezeEntry entry = kvp.Value;

            if (entry.IsInfinite)
            {
                continue;
            }

            entry.RemainingSeconds -= deltaTime;

            if (entry.RemainingSeconds <= 0f)
            {
                _expiredKeys.Add(key);
                anyExpired = true;
            }
            else
            {
                _freezeStatuses[key] = entry;
            }
        }

        foreach (var key in _expiredKeys)
        {
            _freezeStatuses.Remove(key);
        }

        return anyExpired;
    }

    private void TickDotStatuses(float deltaTime)
    {
        if (_dotStatuses.Count == 0)
        {
            return;
        }

        _expiredKeys.Clear();

        foreach (var kvp in _dotStatuses)
        {
            string key = kvp.Key;
            DotEntry entry = kvp.Value;

            entry.TickTimer += deltaTime;

            // TickIntervalSeconds는 [Min(0.01f)]이지만 이는 인스펙터 클램프일 뿐이라,
            // 생성기 등이 SerializedProperty로 0을 직접 써넣으면 그대로 통과한다 - 0 이하를
            // while 조건에 넣으면 에디터가 멈추므로 반드시 가드한다.
            if (entry.Source.TickIntervalSeconds > 0f)
            {
                while (entry.TickTimer >= entry.Source.TickIntervalSeconds)
                {
                    entry.TickTimer -= entry.Source.TickIntervalSeconds;
                    _monster?.TakeDamage(new DamageInfo(entry.Source.DamagePerTick, entry.Element));
                }
            }

            if (!entry.IsInfinite)
            {
                entry.RemainingSeconds -= deltaTime;

                if (entry.RemainingSeconds <= 0f)
                {
                    _expiredKeys.Add(key);
                    continue;
                }
            }

            _dotStatuses[key] = entry;
        }

        foreach (var key in _expiredKeys)
        {
            _dotStatuses.Remove(key);
        }
    }

    private static string ResolveKey(StatusEffectSO status) =>
        string.IsNullOrEmpty(status.StatusId) ? status.GetInstanceID().ToString() : status.StatusId;

    public bool HasStatus (string statusId)
    {
        if (string.IsNullOrEmpty(statusId))
        {
            return false;
        }

        return _moveSpeedStatuses.ContainsKey(statusId) ||
            _freezeStatuses.ContainsKey(statusId) ||
            _dotStatuses.ContainsKey(statusId) ||
            _stackStatuses.ContainsKey(statusId);
    }
}
