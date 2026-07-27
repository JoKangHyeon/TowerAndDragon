using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;

/// <summary>
/// 웨이브 실행을 조율한다. 실제 실행 로직은 데이터 계약 검증 후 구현한다.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [SerializeField] private List<Portal> _portals;
    [SerializeField] private EnemyEnhancementManager _enemyEnhancementManager;
    [SerializeField] private UnityEvent _allSpawnCompleted;
    [SerializeField] private UnityEvent _allMonstersDefeated;


    private CancellationTokenSource _waveCancellation;
    private readonly Dictionary<PortalDirection, Portal> _portalById = new();
    private readonly List<BaseMonster> _spawnedMonsters = new();
    private bool _isDebugCompletionRequested;
    private bool _hasCompletionEventBeenRaised;

    public bool IsRunning { get; private set; }
    public UnityEvent AllSpawnsCompleted => _allSpawnCompleted;
    public UnityEvent AllMonstersDefeated => _allMonstersDefeated;

    public async UniTask StartWaveAsync(WaveDefinitionSO waveDefinition)
    {
        if (IsRunning)
        {
            Debug.LogWarning("[WaveManager] 이미 웨이브가 실행 중입니다.", this);
            return;
        }

        if (waveDefinition == null)
        {
            Debug.LogError("[WaveManager] 실행할 웨이브 데이터가 없습니다.", this);
            return;
        }

        if (!TryBuildPortalMap())
        {
            return;
        }

        if (_enemyEnhancementManager == null)
        {
            Debug.LogWarning(
                "[WaveManager] EnemyEnhancementManager 참조가 없어 기본 웨이브만 실행합니다.",
                this);
        }

        List<RuntimePortalWave> runtimeWaves = new();

        foreach (PortalWaveData portalWave in waveDefinition.PortalWaves)
        {
            if (portalWave == null)
            {
                Debug.LogError("[WaveManager] 비어 있는 포탈 편성이 있습니다.", this);
                return;
            }

            if (!TryGetPortal(portalWave, out Portal portal))
            {
                return;
            }

            if (!portal.IsActive)
            {
                continue;
            }

            runtimeWaves.Add(BuildRuntimePortalWave(portalWave, portal));
        }

        LogWaveComposition(waveDefinition, runtimeWaves);
        _spawnedMonsters.Clear();

        _waveCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            destroyCancellationToken);

        CancellationTokenSource currentCancellation = _waveCancellation;
        IsRunning = true;
        _isDebugCompletionRequested = false;
        _hasCompletionEventBeenRaised = false;

        try
        {
            List<UniTask> portalTasks = new();

            foreach (RuntimePortalWave runtimeWave in runtimeWaves)
            {
                portalTasks.Add(SpawnPortalWaveAsync(
                    runtimeWave,
                    currentCancellation.Token));
            }

            await UniTask.WhenAll(portalTasks);
            _allSpawnCompleted?.Invoke();

            await UniTask.WaitUntil(
                AreAllMonstersDefeated,
                cancellationToken: currentCancellation.Token
            );

            RaiseAllMonstersDefeated();
        }
        catch (OperationCanceledException)
        {
            // 웨이브 취소는 정상적인 실행 종료 흐름이다.
        }
        finally
        {
            if (_waveCancellation == currentCancellation)
            {
                _waveCancellation = null;
            }

            currentCancellation.Dispose();
            IsRunning = false;
            _isDebugCompletionRequested = false;
        }
    }

    public void CancelWave()
    {
        _waveCancellation?.Cancel();
    }

    /// <summary>
    /// 테스트를 위해 진행 중인 웨이브를 즉시 완료한다.
    /// 남은 스폰을 취소하고 이미 생성된 몬스터를 사망 처리한 뒤 기존 완료 이벤트를 발행한다.
    /// </summary>
    public void DebugForceCompleteWave()
    {
        if (!IsRunning || _isDebugCompletionRequested)
        {
            return;
        }

        _isDebugCompletionRequested = true;
        _waveCancellation?.Cancel();

        foreach (BaseMonster monster in _spawnedMonsters)
        {
            if (monster == null || monster.IsDead)
            {
                continue;
            }

            monster.DebugDefeatImmediately();
        }

        _spawnedMonsters.Clear();
        RaiseAllMonstersDefeated();
    }

    private void RaiseAllMonstersDefeated()
    {
        if (_hasCompletionEventBeenRaised)
        {
            return;
        }

        _hasCompletionEventBeenRaised = true;
        _allMonstersDefeated?.Invoke();
    }

    private bool TryBuildPortalMap()
    {
        _portalById.Clear();

        bool isValid = true;

        foreach (Portal portal in _portals)
        {
            if (portal == null)
            {
                Debug.LogError("[WaveManager] 포탈 참조가 비어 있습니다.", this);
                isValid = false;
                continue;
            }

            if (!portal.IsConfigured)
            {
                Debug.LogError($"[WaveManager] {portal.name} 포탈 설정이 올바르지 않습니다.", portal);
                isValid = false;
                continue;
            }

            if (!_portalById.TryAdd(portal.PortalDirectionId, portal))
            {
                Debug.LogError($"[WaveManager] {portal.PortalDirectionId} 포탈 ID가 중복되었습니다.", portal);
                isValid = false;
            }
        }

        return isValid;
    }

    private bool TryGetPortal(PortalWaveData portalWave, out Portal portal)
    {
        if (_portalById.TryGetValue(portalWave.PortalDirectionId, out portal))
        {
            return true;
        }

        Debug.LogError(
            $"[WaveManager] {portalWave.PortalDirectionId} 포탈이 씬에 없습니다.",
            this);

        return false;
    }

    private RuntimePortalWave BuildRuntimePortalWave(
        PortalWaveData portalWave,
        Portal portal)
    {
        IReadOnlyList<EnemyEnhancementProfileSO> profiles =
            _enemyEnhancementManager != null
                ? _enemyEnhancementManager.GetProfiles(portal.TerrainType)
                : Array.Empty<EnemyEnhancementProfileSO>();

        List<RuntimeSpawnGroup> runtimeGroups = new();
        HashSet<MonsterData> monstersWithAllocatedSpawnBonus = new();

        foreach (SpawnGroupData spawnGroup in portalWave.SpawnGroups)
        {
            EnemyEnhancementSnapshot enhancement =
                EnemyEnhancementResolver.Resolve(profiles, spawnGroup.MonsterData);

            int spawnCountBonus = monstersWithAllocatedSpawnBonus.Add(
                spawnGroup.MonsterData)
                    ? enhancement.SpawnCountBonus
                    : 0;

            int spawnCount = Mathf.Max(
                0,
                spawnGroup.SpawnCount + spawnCountBonus);

            float spawnInterval = Mathf.Max(
                0f,
                enhancement.SpawnInterval.Apply(spawnGroup.SpawnInterval));

            runtimeGroups.Add(new RuntimeSpawnGroup(
                spawnGroup,
                enhancement,
                spawnCount,
                spawnInterval));
        }

        return new RuntimePortalWave(portalWave, portal, runtimeGroups);
    }

    private async UniTask SpawnPortalWaveAsync(
        RuntimePortalWave runtimeWave,
        CancellationToken token)
    {
        if (runtimeWave.Source.StartDelay > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(runtimeWave.Source.StartDelay),
                cancellationToken: token);
        }

        foreach (RuntimeSpawnGroup spawnGroup in runtimeWave.SpawnGroups)
        {
            token.ThrowIfCancellationRequested();

            await SpawnGroupAsync(spawnGroup, runtimeWave.Portal, token);
        }
    }

    private async UniTask SpawnGroupAsync(
        RuntimeSpawnGroup runtimeGroup,
        Portal portal,
        CancellationToken token)
    {
        if (runtimeGroup.Source.DelayBeforeGroup > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(runtimeGroup.Source.DelayBeforeGroup),
                cancellationToken: token);
        }

        for (int i = 0; i < runtimeGroup.SpawnCount; i++)
        {
            token.ThrowIfCancellationRequested();

            SpawnMonster(runtimeGroup, portal);

            bool hasNextMonster = i + 1 < runtimeGroup.SpawnCount;

            if (hasNextMonster && runtimeGroup.SpawnInterval > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(runtimeGroup.SpawnInterval),
                    cancellationToken: token);
            }
        }
    }

    private BaseMonster SpawnMonster(
        RuntimeSpawnGroup runtimeGroup,
        Portal portal)
    {
        BaseMonster monster = Instantiate(
            runtimeGroup.Source.MonsterPrefab,
            portal.SpawnPoint.position,
            portal.SpawnPoint.rotation,
            transform);

        monster.Setup(
            runtimeGroup.Source.MonsterData,
            portal.GroundPath,
            portal.MainCastle,
            runtimeGroup.Enhancement);

        _spawnedMonsters.Add(monster);

        return monster;
    }

    private bool AreAllMonstersDefeated()
    {
        _spawnedMonsters.RemoveAll(
            monster => monster == null || monster.IsDead);

        return _spawnedMonsters.Count == 0;
    }

    // 점령 페널티 적용 후 실제 실행되는 웨이브 편성 로그
    private void LogWaveComposition(
        WaveDefinitionSO waveDefinition,
        IReadOnlyList<RuntimePortalWave> portalWaves)
    {
        int totalSpawnCount = 0;

        foreach (RuntimePortalWave portalWave in portalWaves)
        {
            foreach (RuntimeSpawnGroup spawnGroup in portalWave.SpawnGroups)
            {
                totalSpawnCount += spawnGroup.SpawnCount;

                Debug.Log(
                    $"[WaveManager] Wave={waveDefinition.name}, Portal={portalWave.Portal.name}, " +
                    $"Terrain={portalWave.Portal.TerrainType}, Monster={spawnGroup.Source.MonsterData.name}, " +
                    $"Base={spawnGroup.Source.SpawnCount}, Bonus={spawnGroup.SpawnCountBonus}, " +
                    $"Final={spawnGroup.SpawnCount}, Interval={spawnGroup.SpawnInterval}",
                    this);
            }
        }

        Debug.Log(
            $"[WaveManager] Wave={waveDefinition.name}, TotalSpawnCount={totalSpawnCount}",
            this);
    }

    private sealed class RuntimePortalWave
    {
        public PortalWaveData Source { get; }
        public Portal Portal { get; }
        public IReadOnlyList<RuntimeSpawnGroup> SpawnGroups { get; }

        public RuntimePortalWave(
            PortalWaveData source,
            Portal portal,
            IReadOnlyList<RuntimeSpawnGroup> spawnGroups)
        {
            Source = source;
            Portal = portal;
            SpawnGroups = spawnGroups;
        }
    }

    private sealed class RuntimeSpawnGroup
    {
        public SpawnGroupData Source { get; }
        public EnemyEnhancementSnapshot Enhancement { get; }
        public int SpawnCount { get; }
        public float SpawnInterval { get; }
        public int SpawnCountBonus => SpawnCount - Source.SpawnCount;

        public RuntimeSpawnGroup(
            SpawnGroupData source,
            EnemyEnhancementSnapshot enhancement,
            int spawnCount,
            float spawnInterval)
        {
            Source = source;
            Enhancement = enhancement;
            SpawnCount = spawnCount;
            SpawnInterval = spawnInterval;
        }
    }
}
