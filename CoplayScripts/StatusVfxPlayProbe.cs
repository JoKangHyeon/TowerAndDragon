using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 플레이 모드에서 상태 지속 연출의 수명을 실제로 돌려 보는 하네스.
///
/// 밤 웨이브를 기다려 불 타워 사거리를 맞추는 대신, 몬스터에 직접 상태를 걸어
/// 부여 → 추종 → 재부여 → 만료 → 사망 정리를 순서대로 관찰한다.
///
/// EditorApplication.update로 단계를 넘긴다 - 에디터 전용 스크립트라 씬에 MonoBehaviour를
/// 심지 않아도 되고, 플레이 모드가 도는 동안 프레임마다 불린다.
/// </summary>
public static class StatusVfxPlayProbe
{
    private const string POOL_OBJECT_NAME = "ProjectilePool (auto)";
    private const string FIRE_BURN_ASSET_PATH =
        "Assets/Data/TowerData/ElementalTower/Fire/TS_FireBurn.asset";
    private const string EFFECT_NAME_MARKER = "FX_Status";

    // 낮에 실행했을 때 세울 대상. 루트 스케일 1이라 몸통 위치를 눈대중으로 검산하기 쉽다.
    private const string PROBE_MONSTER_PREFAB_PATH =
        "Assets/Data/MonsterData/MonsterPrefab/MP_Ground_Tank.prefab";
    private const string PROBE_MONSTER_NAME = "StatusVfxProbeTarget (temp)";

    // 대상이 파괴된 뒤를 관찰하는 마지막 단계.
    private const int LAST_STEP = 6;

    private const float MOVE_STEP = 0.35f;
    private const float SETTLE_SECONDS = 0.1f;
    private const float EXPIRY_MARGIN_SECONDS = 0.5f;

    private static StatusEffectSO _status;
    private static BaseMonster _monster;
    private static Transform _effect;
    private static StringBuilder _report;

    private static int _step;
    private static double _resumeAt;
    private static Vector3 _effectBeforeMove;

    // 프로브가 직접 세운 대상인지. 마지막 단계에서 Kill로 정리되지만, 중간에 끊긴 경우
    // 씬에 임시 몬스터를 남기지 않으려면 알고 있어야 한다.
    private static bool _spawnedForProbe;

    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[StatusVfxPlayProbe] 플레이 모드에서 실행해야 합니다.");
            return;
        }

        _status = AssetDatabase.LoadAssetAtPath<StatusEffectSO>(FIRE_BURN_ASSET_PATH);
        _report = new StringBuilder();
        _step = 0;
        _resumeAt = 0d;

        _report.AppendLine("[StatusVfxPlayProbe]");

        if (_status == null)
        {
            _report.AppendLine("  실패: TS_FireBurn을 불러오지 못했습니다.");
            Finish();
            return;
        }

        _monster = Object.FindFirstObjectByType<BaseMonster>();

        // 낮에는 씬에 몬스터가 없다. 웨이브를 돌리면 다른 연출이 섞여 관찰이 흐려지므로
        // 대상 하나만 직접 세운다 - Setup(스플라인·성)은 부르지 않는다. 상태 수신기는 그와 무관하게
        // Awake만으로 동작하고, 여기서 확인하려는 것은 이동이 아니라 연출의 부여·추종·반납이다.
        if (_monster == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PROBE_MONSTER_PREFAB_PATH);

            if (prefab == null)
            {
                _report.AppendLine($"  실패: 프리팹을 찾지 못했습니다: {PROBE_MONSTER_PREFAB_PATH}");
                Finish();
                return;
            }

            GameObject spawned = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
            spawned.name = PROBE_MONSTER_NAME;
            _monster = spawned.GetComponent<BaseMonster>();
            _spawnedForProbe = true;

            _report.AppendLine($"  대상 없음 → {prefab.name}을 직접 세웠습니다.");
        }

        _report.AppendLine(
            $"  상태: {_status.name} 지속={_status.DurationSeconds}s " +
            $"연출={(_status.HasActiveVfx ? _status.ActiveVfxPrefab.name : "<없음>")}");
        _report.AppendLine(
            $"  대상: {_monster.name} (scale={_monster.transform.localScale.x})");

        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!Application.isPlaying)
        {
            _report.AppendLine("  중단: 플레이 모드가 끝났습니다.");
            Finish();
            return;
        }

        if (EditorApplication.timeSinceStartup < _resumeAt)
        {
            return;
        }

        // 몬스터가 도중에 사라지면(도착·사망) 남은 단계를 진행할 수 없다.
        // 마지막 단계는 예외다 - 거기서는 대상이 사라진 것이 관찰 대상 자체다.
        if (_monster == null && _step < LAST_STEP)
        {
            _report.AppendLine($"  중단: 대상 몬스터가 {_step}단계에서 사라졌습니다.");
            Finish();
            return;
        }

        switch (_step)
        {
            case 0:
                _report.AppendLine($"  [0] 부여 전 활성 연출 = {CountActiveEffects()}");
                _monster.ApplyStatus(_status);
                Wait(SETTLE_SECONDS);
                break;

            case 1:
                _report.AppendLine($"  [1] 부여 후 활성 연출 = {CountActiveEffects()} (1이어야 함)");
                _effect = FindFirstActiveEffect();

                if (_effect == null)
                {
                    _report.AppendLine("  실패: 연출이 켜지지 않았습니다.");
                    Finish();
                    return;
                }

                _effectBeforeMove = _effect.position;
                _monster.transform.position += new Vector3(MOVE_STEP, 0f, 0f);
                Wait(SETTLE_SECONDS);
                break;

            case 2:
                LogFollow();
                _monster.ApplyStatus(_status);
                Wait(SETTLE_SECONDS);
                break;

            case 3:
                _report.AppendLine($"  [3] 재부여 후 활성 연출 = {CountActiveEffects()} (1이어야 함)");
                Wait(_status.DurationSeconds + EXPIRY_MARGIN_SECONDS);
                break;

            case 4:
                _report.AppendLine($"  [4] 만료 후 활성 연출 = {CountActiveEffects()} (0이어야 함)");
                _monster.ApplyStatus(_status);
                Wait(SETTLE_SECONDS);
                break;

            case 5:
                _report.AppendLine($"  [5] 사망 직전 활성 연출 = {CountActiveEffects()} (1이어야 함)");

                // Kill()이 아니라 Destroy를 부른다. 사망 리스너(_health.Died → HandleDeath)는
                // Setup 안에서 붙는데 이 프로브는 Setup을 건너뛰므로 Kill로는 파괴가 일어나지 않는다.
                // 제품의 HandleDeath가 하는 일이 Destroy(gameObject)이므로 그 지점을 직접 재현한다.
                Object.Destroy(_monster.gameObject);
                Wait(SETTLE_SECONDS);
                break;

            default:
                _report.AppendLine($"  [6] 사망 후 활성 연출 = {CountActiveEffects()} (0이어야 함)");
                Finish();
                return;
        }

        _step++;
    }

    private static void LogFollow()
    {
        if (_effect == null)
        {
            _report.AppendLine("  [2] 연출이 도중에 사라졌습니다.");
            return;
        }

        float followDelta = _effect.position.x - _effectBeforeMove.x;

        _report.AppendLine(
            $"  [2] 몬스터 {MOVE_STEP} 이동 → 연출 이동 {followDelta:0.###} " +
            $"({MOVE_STEP}에 가까워야 함)");
        _report.AppendLine(
            $"      연출 위치 y={_effect.position.y:0.##}, " +
            $"몬스터 y={_monster.transform.position.y:0.##} (몸통이면 연출이 조금 위)");
    }

    private static void Wait(float seconds)
    {
        _resumeAt = EditorApplication.timeSinceStartup + seconds;
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;

        // 중간에 끊겨 Kill 단계까지 못 갔으면 임시 대상이 씬에 남는다.
        if (_spawnedForProbe && _monster != null)
        {
            Object.Destroy(_monster.gameObject);
            _report.AppendLine("  정리: 임시 대상을 제거했습니다.");
        }

        Debug.Log(_report.ToString());

        _status = null;
        _monster = null;
        _effect = null;
        _spawnedForProbe = false;
    }

    // 풀 밑에서 활성인 상태 연출만 센다 - 반납된 것은 비활성으로 남아 있다.
    private static int CountActiveEffects()
    {
        Transform pool = ResolvePool();

        if (pool == null)
        {
            return 0;
        }

        int count = 0;

        foreach (Transform child in pool)
        {
            if (IsActiveStatusEffect(child))
            {
                count++;
            }
        }

        return count;
    }

    private static Transform FindFirstActiveEffect()
    {
        Transform pool = ResolvePool();

        if (pool == null)
        {
            return null;
        }

        foreach (Transform child in pool)
        {
            if (IsActiveStatusEffect(child))
            {
                return child;
            }
        }

        return null;
    }

    private static bool IsActiveStatusEffect(Transform candidate) =>
        candidate.gameObject.activeSelf && candidate.name.Contains(EFFECT_NAME_MARKER);

    private static Transform ResolvePool()
    {
        GameObject pool = GameObject.Find(POOL_OBJECT_NAME);

        return pool != null ? pool.transform : null;
    }
}
