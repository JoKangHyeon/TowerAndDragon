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
    // Element는 보관하지 않는다 - 스택은 임계치에 도달하는 순간 그 자리에서 곧바로
    // TriggerStatus를 부여하므로 나중에 되읽을 일이 없다.
    // Source는 툴팁이 이름과 임계치("냉기 중첩 2 / 3")를 적기 위해 보관한다.
    private struct StackEntry
    {
        public StackingStatusEffectSO Source;
        public bool IsInfinite;
        public float RemainingSeconds;
        public int Stacks;
    }

    // 남은 시간은 소수점 한 자리까지만 적는다 - 툴팁을 0.25초마다 갱신하므로 그보다 잘게 적으면
    // 숫자가 쉴 새 없이 흔들려 읽을 수 없다.
    private const string REMAINING_SECONDS_FORMAT = "0.#";

    private readonly Dictionary<string, MoveSpeedEntry> _moveSpeedStatuses = new();
    private readonly Dictionary<string, DotEntry> _dotStatuses = new();
    private readonly Dictionary<string, StackEntry> _stackStatuses = new();
    private readonly Dictionary<string, FreezeEntry> _freezeStatuses = new();
    private readonly List<string> _tickKeys = new(4);

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
        if (status == null || IsImmuneTo(status))
        {
            return;
        }

        switch (status)
        {
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

    // 부여 시점에 단 한 곳에서 걸러낸다 - 이미 보관 중인 엔트리를 매 틱 다시 판정하지 않아도 되고,
    // 스킬·타워·새끼용 등 모든 부여 경로가 결국 여기로 모이므로 누락이 생기지 않는다.
    // PiercesCrowdControlImmunity가 켜진 상태(어미용 액티브 전역 빙결)는 면역을 뚫고 그대로 걸린다.
    private bool IsImmuneTo(StatusEffectSO status) =>
        status.IsCrowdControl &&
        !status.PiercesCrowdControlImmunity &&
        _monster != null &&
        _monster.IsCrowdControlImmune;

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
            Source = status,
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

    // Unity(Mono) 런타임의 Dictionary는 "기존 키에 값을 다시 대입"해도 내부 version이 올라가서
    // 열려 있던 enumerator가 무효화된다(version을 올리지 않는 것은 .NET Core 3.0 이후 런타임 얘기다).
    // 그래서 틱 처리 중에는 딕셔너리를 직접 순회하지 않고, 키만 버퍼에 복사한 뒤 그 버퍼를 돌면서
    // 값을 갱신·삭제한다. 만료 항목도 그 자리에서 바로 지울 수 있다.
    private void CollectKeys<TEntry>(Dictionary<string, TEntry> statuses)
    {
        _tickKeys.Clear();

        foreach (string key in statuses.Keys)
        {
            _tickKeys.Add(key);
        }
    }

    private void TickStackStatuses (float deltaTime)
    {
        if (_stackStatuses.Count == 0)
        {
            return;
        }

        CollectKeys(_stackStatuses);

        foreach (string key in _tickKeys)
        {
            if (!_stackStatuses.TryGetValue(key, out StackEntry entry) || entry.IsInfinite)
            {
                continue;
            }

            entry.RemainingSeconds -= deltaTime;

            // 유지 시간이 지나면 쌓인 스택을 통째로 비운다 (1개씩 감소가 아님)
            // 마지막 타격 이휴 5초안에 3번을 채워야 빙결
            if (entry.RemainingSeconds <= 0f)
            {
                _stackStatuses.Remove(key);
            }
            else
            {
                _stackStatuses[key] = entry;
            }
        }
    }

    private bool TickMoveSpeedStatuses(float deltaTime)
    {
        if (_moveSpeedStatuses.Count == 0)
        {
            return false;
        }

        CollectKeys(_moveSpeedStatuses);
        bool anyExpired = false;

        foreach (string key in _tickKeys)
        {
            if (!_moveSpeedStatuses.TryGetValue(key, out MoveSpeedEntry entry) || entry.IsInfinite)
            {
                continue;
            }

            entry.RemainingSeconds -= deltaTime;

            if (entry.RemainingSeconds <= 0f)
            {
                _moveSpeedStatuses.Remove(key);
                anyExpired = true;
            }
            else
            {
                _moveSpeedStatuses[key] = entry;
            }
        }

        return anyExpired;
    }

    private bool TickFreezeStatuses(float deltaTime)
    {
        if (_freezeStatuses.Count == 0)
        {
            return false;
        }

        CollectKeys(_freezeStatuses);
        bool anyExpired = false;

        foreach (string key in _tickKeys)
        {
            if (!_freezeStatuses.TryGetValue(key, out FreezeEntry entry) || entry.IsInfinite)
            {
                continue;
            }

            entry.RemainingSeconds -= deltaTime;

            if (entry.RemainingSeconds <= 0f)
            {
                _freezeStatuses.Remove(key);
                anyExpired = true;
            }
            else
            {
                _freezeStatuses[key] = entry;
            }
        }

        return anyExpired;
    }

    private void TickDotStatuses(float deltaTime)
    {
        if (_dotStatuses.Count == 0)
        {
            return;
        }

        CollectKeys(_dotStatuses);

        foreach (string key in _tickKeys)
        {
            if (!_dotStatuses.TryGetValue(key, out DotEntry entry))
            {
                continue;
            }

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
                    _dotStatuses.Remove(key);
                    continue;
                }
            }

            // 틱 데미지로 몬스터가 죽어 Clear()가 돌았다면 이 항목은 이미 사라진 상태다 -
            // 조건 없이 되쓰면 지워진 상태이상을 되살린다.
            if (_dotStatuses.ContainsKey(key))
            {
                _dotStatuses[key] = entry;
            }
        }
    }

    /// <summary>
    /// 지금 걸려 있는 상태를 툴팁용 줄로 만들어 buffer에 덧붙인다(비우지 않는다 -
    /// 전역 줄과 한 목록에 모으기 때문이다).
    ///
    /// 딕셔너리를 그대로 노출하지 않고 호출자가 준 버퍼를 채우는 이유는 두 가지다.
    /// 만료·재적용을 이 컴포넌트만 관리한다는 캡슐화를 지키고, 툴팁이 0.25초마다 다시 만드는
    /// 목록이라 매번 새 컬렉션을 할당하지 않기 위해서다.
    /// </summary>
    public void CollectActiveStatuses(List<MonsterStatusLine> buffer)
    {
        if (buffer == null)
        {
            return;
        }

        // 빙결 중에는 둔화를 적지 않는다 - MoveSpeedMultiplier가 이미 빙결을 우선해 0으로 만들므로,
        // 둘 다 적으면 지금 효과가 없는 둔화가 작동 중인 것처럼 읽힌다.
        bool isFrozen = _freezeStatuses.Count > 0;

        foreach (FreezeEntry entry in _freezeStatuses.Values)
        {
            AppendLine(buffer, entry.Source, entry.IsInfinite, entry.RemainingSeconds);
        }

        if (!isFrozen)
        {
            foreach (MoveSpeedEntry entry in _moveSpeedStatuses.Values)
            {
                AppendLine(buffer, entry.Source, entry.IsInfinite, entry.RemainingSeconds);
            }
        }

        foreach (DotEntry entry in _dotStatuses.Values)
        {
            AppendLine(buffer, entry.Source, entry.IsInfinite, entry.RemainingSeconds);
        }

        foreach (StackEntry entry in _stackStatuses.Values)
        {
            AppendStackLine(buffer, entry);
        }
    }

    // 이름 키가 없는 상태는 줄을 만들지 않는다(StatusEffectSO.HasDisplayName 주석 참고).
    private static void AppendLine(
        List<MonsterStatusLine> buffer,
        StatusEffectSO source,
        bool isInfinite,
        float remainingSeconds)
    {
        if (source == null || !source.HasDisplayName)
        {
            return;
        }

        buffer.Add(new MonsterStatusLine(
            MonsterStatusScope.Instance,
            StringTable.GetString(source.DisplayNameLocKey),
            FormatRemaining(isInfinite, remainingSeconds)));
    }

    // 스택은 남은 시간이 아니라 "몇 개를 쌓았는지"가 판단 근거다 - 임계치까지 한 번 더 때리면
    // 되는지가 보여야 한다.
    private static void AppendStackLine(List<MonsterStatusLine> buffer, StackEntry entry)
    {
        if (entry.Source == null || !entry.Source.HasDisplayName)
        {
            return;
        }

        buffer.Add(new MonsterStatusLine(
            MonsterStatusScope.Instance,
            StringTable.GetString(entry.Source.DisplayNameLocKey),
            string.Format(
                StringTable.GetString(StatusLocKeys.ROW_STACKS),
                entry.Stacks,
                entry.Source.StacksToTrigger)));
    }

    private static string FormatRemaining(bool isInfinite, float remainingSeconds)
    {
        if (isInfinite)
        {
            return StringTable.GetString(StatusLocKeys.ROW_INFINITE);
        }

        return string.Format(
            StringTable.GetString(StatusLocKeys.ROW_DURATION),
            remainingSeconds.ToString(REMAINING_SECONDS_FORMAT));
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
