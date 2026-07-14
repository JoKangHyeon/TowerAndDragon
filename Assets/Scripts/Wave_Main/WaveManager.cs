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
    [SerializeField] private List<Portal> _portals = new();
    [SerializeField] private UnityEvent _allSpawnCompleted = new();

    private CancellationTokenSource _waveCancellation;
    private readonly Dictionary<PortalId, Portal> _portalById = new();

    public bool IsRunning { get; private set; }
    public UnityEvent AllSpawnsCompleted => _allSpawnCompleted;

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

        List<PortalWaveData> executableWaves = new();
        List<Portal> executablePortals = new();

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

            executableWaves.Add(portalWave);
            executablePortals.Add(portal);
        }

        _waveCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            destroyCancellationToken);

        CancellationTokenSource currentCancellation = _waveCancellation;
        IsRunning = true;

        try
        {
            List<UniTask> portalTasks = new();

            for (int i = 0; i < executableWaves.Count; i++)
            {
                portalTasks.Add(
                    SpawnPortalWaveAsync(
                        executableWaves[i],
                        executablePortals[i],
                        currentCancellation.Token));
            }

            await UniTask.WhenAll(portalTasks);
            _allSpawnCompleted?.Invoke();
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
        }
    }

    public void CancelWave()
    {
        _waveCancellation?.Cancel();
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

            if (!_portalById.TryAdd(portal.Id, portal))
            {
                Debug.LogError($"[WaveManager] {portal.Id} 포탈 ID가 중복되었습니다.", portal);
                isValid = false;
            }
        }

        return isValid;
    }

    private bool TryGetPortal(PortalWaveData portalWave, out Portal portal)
    {
        if (_portalById.TryGetValue(portalWave.PortalId, out portal))
        {
            return true;
        }

        Debug.LogError(
            $"[WaveManager] {portalWave.PortalId} 포탈이 씬에 없습니다.",
            this);

        return false;
    }

    private async UniTask SpawnPortalWaveAsync(
        PortalWaveData portalWave,
        Portal portal,
        CancellationToken token)
    {
        if (portalWave.StartDelay > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(portalWave.StartDelay),
                cancellationToken: token);
        }

        foreach (SpawnGroupData spawnGroup in portalWave.SpawnGroups)
        {
            token.ThrowIfCancellationRequested();

            await SpawnGroupAsync(spawnGroup, portal, token);
        }
    }

    private async UniTask SpawnGroupAsync(
        SpawnGroupData spawnGroup,
        Portal portal,
        CancellationToken token)
    {
        if (spawnGroup.DelayBeforeGroup > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(spawnGroup.DelayBeforeGroup),
                cancellationToken: token);
        }

        for (int i = 0; i < spawnGroup.SpawnCount; i++)
        {
            token.ThrowIfCancellationRequested();

            SpawnMonster(spawnGroup, portal);

            bool hasNextMonster = i + 1 < spawnGroup.SpawnCount;

            if (hasNextMonster && spawnGroup.SpawnInterval > 0f)
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(spawnGroup.SpawnInterval),
                    cancellationToken: token);
            }
        }
    }

    private BaseMonster SpawnMonster(
        SpawnGroupData spawnGroup,
        Portal portal)
    {
        BaseMonster monster = Instantiate(
            spawnGroup.MonsterPrefab,
            portal.SpawnPoint.position,
            portal.SpawnPoint.rotation,
            transform);

        monster.Setup(
            spawnGroup.MonsterData,
            portal.GroundPath,
            portal.MainCastle);

        return monster;
    }
}
