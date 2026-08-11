using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines;
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
    [SerializeField] private UnityEvent<BaseMonster> _monsterSpawned;


    private CancellationTokenSource _waveCancellation;
    private readonly Dictionary<PortalDirection, Portal> _portalById = new();
    private readonly List<BaseMonster> _spawnedMonsters = new();
    private bool _isDebugCompletionRequested;
    private bool _hasCompletionEventBeenRaised;

    public bool IsRunning { get; private set; }
    public UnityEvent AllSpawnsCompleted => _allSpawnCompleted;
    public UnityEvent AllMonstersDefeated => _allMonstersDefeated;
    public UnityEvent<BaseMonster> MonsterSpawned => _monsterSpawned;

    // 용 스킬트리 전역형 액티브(빙결·전역 대미지)가 대상 목록을 얻는 데 쓴다.
    public IReadOnlyList<BaseMonster> SpawnedMonsters => _spawnedMonsters;

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

        List<PortalRoutePlan> portalPlans = new();

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

            PortalRoutePlan portalPlan = WaveRoutePlanner.BuildPortalPlan(
                portalWave,
                portal,
                _enemyEnhancementManager);

            if (portalPlan.HasSpawns)
            {
                portalPlans.Add(portalPlan);
            }
        }

        LogWaveComposition(waveDefinition, portalPlans);
        _spawnedMonsters.Clear();

        _waveCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            destroyCancellationToken);

        CancellationTokenSource currentCancellation = _waveCancellation;
        IsRunning = true;
        _isDebugCompletionRequested = false;
        _hasCompletionEventBeenRaised = false;

        try
        {
            List<UniTask> routeTasks = new();

            // 루트마다 독립적으로 진행한다. 포탈 지연 위에 루트 지연이 더해진다.
            foreach (PortalRoutePlan portalPlan in portalPlans)
            {
                foreach (RouteSpawnPlan routePlan in portalPlan.Routes)
                {
                    routeTasks.Add(SpawnRouteAsync(
                        portalPlan,
                        routePlan,
                        currentCancellation.Token));
                }
            }

            await UniTask.WhenAll(routeTasks);
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

    /// <summary>[테스트 전용] F 키 등으로 진행 중인 웨이브를 즉시 끝낸다.</summary>
    public void DebugForceCompleteWave() => ForceCompleteWave();

    /// <summary>
    /// 진행 중인 웨이브를 즉시 완료한다. 남은 스폰을 취소하고 이미 생성된 몬스터를 사망 처리한 뒤
    /// 기존 완료 이벤트를 발행한다 - 밤을 끝내는 경로가 그 이벤트뿐이므로, 몬스터를 잡을 수단이
    /// 없어 밤이 끝나지 않는 상황(튜토리얼 구제 등)을 푸는 데도 쓴다.
    /// </summary>
    public void ForceCompleteWave()
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

    private async UniTask SpawnRouteAsync(
        PortalRoutePlan portalPlan,
        RouteSpawnPlan routePlan,
        CancellationToken token)
    {
        float startDelay = portalPlan.Source.StartDelay + routePlan.StartDelay;

        if (startDelay > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(startDelay),
                cancellationToken: token);
        }

        foreach (RuntimeSpawnGroup spawnGroup in routePlan.SpawnGroups)
        {
            token.ThrowIfCancellationRequested();

            await SpawnGroupAsync(spawnGroup, portalPlan.Portal, routePlan.Path, token);
        }
    }

    private async UniTask SpawnGroupAsync(
        RuntimeSpawnGroup runtimeGroup,
        Portal portal,
        SplineContainer path,
        CancellationToken token)
    {
        if (runtimeGroup.DelayBeforeGroup > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(runtimeGroup.DelayBeforeGroup),
                cancellationToken: token);
        }

        for (int i = 0; i < runtimeGroup.SpawnCount; i++)
        {
            token.ThrowIfCancellationRequested();

            SpawnMonster(runtimeGroup, portal, path);

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
        Portal portal,
        SplineContainer path)
    {
        BaseMonster monster = Instantiate(
            runtimeGroup.Source.MonsterPrefab,
            portal.SpawnPoint.position,
            portal.SpawnPoint.rotation,
            transform);

        monster.Setup(
            runtimeGroup.Source.MonsterData,
            path,
            portal.MainCastle,
            runtimeGroup.Enhancement);

        _spawnedMonsters.Add(monster);
        _monsterSpawned?.Invoke(monster);

        return monster;
    }

    private bool AreAllMonstersDefeated()
    {
        _spawnedMonsters.RemoveAll(
            monster => monster == null || monster.IsDead);

        return _spawnedMonsters.Count == 0;
    }

    // 점령 페널티와 루트 배정이 적용된 후 실제 실행되는 웨이브 편성 로그
    private void LogWaveComposition(
        WaveDefinitionSO waveDefinition,
        IReadOnlyList<PortalRoutePlan> portalPlans)
    {
        int totalSpawnCount = 0;

        foreach (PortalRoutePlan portalPlan in portalPlans)
        {
            foreach (RouteSpawnPlan routePlan in portalPlan.Routes)
            {
                totalSpawnCount += routePlan.TotalSpawnCount;

                foreach (RuntimeSpawnGroup spawnGroup in routePlan.SpawnGroups)
                {
                    Debug.Log(
                        $"[WaveManager] Wave={waveDefinition.name}, Portal={portalPlan.Portal.name}, " +
                        $"Route={routePlan.RouteIndex}, Terrain={portalPlan.Portal.TerrainType}, " +
                        $"Monster={spawnGroup.Source.MonsterData.name}, " +
                        $"Bonus={spawnGroup.SpawnCountBonus}, Final={spawnGroup.SpawnCount}, " +
                        $"Interval={spawnGroup.SpawnInterval}",
                        this);
                }
            }

            Debug.Log(
                $"[WaveManager] Portal={portalPlan.Portal.name}, " +
                $"ActiveRoutes={portalPlan.Routes.Count}",
                this);
        }

        Debug.Log(
            $"[WaveManager] Wave={waveDefinition.name}, TotalSpawnCount={totalSpawnCount}",
            this);
    }
}
