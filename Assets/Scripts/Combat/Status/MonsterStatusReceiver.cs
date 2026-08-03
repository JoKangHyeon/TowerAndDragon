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

    private struct DotEntry
    {
        public DamageOverTimeStatusSO Source;
        public DragonType? Element;
        public bool IsInfinite;
        public float RemainingSeconds;
        public float TickTimer;
    }

    private readonly Dictionary<string, MoveSpeedEntry> _moveSpeedStatuses = new();
    private readonly Dictionary<string, DotEntry> _dotStatuses = new();
    private readonly List<string> _keysBuffer = new();

    private BaseMonster _monster;

    // 활성 상태 중 가장 강한 이동속도 배율. 아무 것도 없으면 1(정상 속도).
    public float MoveSpeedMultiplier
    {
        get
        {
            float strongest = 1f;

            foreach (MoveSpeedEntry entry in _moveSpeedStatuses.Values)
            {
                strongest = Mathf.Min(strongest, entry.Source.SpeedMultiplier);
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
            case MoveSpeedStatusSO moveSpeed:
                ApplyMoveSpeed(moveSpeed);
                break;
            case DamageOverTimeStatusSO dot:
                ApplyDot(dot, element);
                break;
        }
    }

    public void Clear()
    {
        _moveSpeedStatuses.Clear();
        _dotStatuses.Clear();
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

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (TickMoveSpeedStatuses(deltaTime))
        {
            _monster?.RefreshMoveSpeed();
        }

        TickDotStatuses(deltaTime);
    }

    // 딕셔너리를 순회하며 값을 갱신/제거하면 열거자가 깨질 수 있어, 키 스냅샷을 먼저 뜬 뒤
    // 그 스냅샷으로 순회한다.
    private bool TickMoveSpeedStatuses(float deltaTime)
    {
        if (_moveSpeedStatuses.Count == 0)
        {
            return false;
        }

        _keysBuffer.Clear();
        _keysBuffer.AddRange(_moveSpeedStatuses.Keys);

        bool anyExpired = false;

        foreach (string key in _keysBuffer)
        {
            MoveSpeedEntry entry = _moveSpeedStatuses[key];

            if (entry.IsInfinite)
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

    private void TickDotStatuses(float deltaTime)
    {
        if (_dotStatuses.Count == 0)
        {
            return;
        }

        _keysBuffer.Clear();
        _keysBuffer.AddRange(_dotStatuses.Keys);

        foreach (string key in _keysBuffer)
        {
            DotEntry entry = _dotStatuses[key];

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

            _dotStatuses[key] = entry;
        }
    }

    private static string ResolveKey(StatusEffectSO status) =>
        string.IsNullOrEmpty(status.StatusId) ? status.GetInstanceID().ToString() : status.StatusId;
}
