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


    public bool IsRunning {get; private set;}   
    public UnityEvent AllSpawnsCompleted => _allSpawnCompleted;

    public UniTask StartWaveAsync (WaveDefinitionSO waveDefinitionSO)
    {
        return UniTask.CompletedTask;
    }

    public void CancelWave()
    {
        
    }

    private bool TryBuildPortalMap()
    {
        _portalById.Clear();

        bool isValid = true;

        foreach (Portal portal in _portals)
        {
            if (portal == null)
            {
                Debug.LogError ($"[WaveManager] 포탈 참조가 비어 있습니다.", this);
                isValid = false;
                continue;
            }

            if (!portal.IsConfigured)
            {
                Debug.LogError ($"[WaveManager] {portal.name} 포탈 설정이 올바르지 않습니다.", portal);
                isValid = false;
                continue;
            }

            if (!_portalById.TryAdd(portal.Id, portal))
            {
                Debug.LogError($"[WaveManager] {portal.Id} 포탈 Id가 중복되었습니다.", portal); 
                isValid = false;
            }
        }

        return isValid;

    }

    private async UniTask SpawnPortalWaveAsync(
        PortalWaveData portalWave,
        Portal portal,
        CancellationToken token
    )
    {
        if (portalWave.StartDelay > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(portalWave.StartDelay),
                cancellationToken : token
            );
        }

        foreach (SpawnGroupData sg in portalWave.SpawnGroups)
        {
            token.ThrowIfCancellationRequested();

            await SpawnGroupAsync(sg, portal, token);
        }
    }

    private async UniTask SpawnGroupAsync(
        SpawnGroupData spawnGroup,
        Portal portal,
        CancellationToken token
    )
    {
        if (spawnGroup.DelayBeforeGroup > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(spawnGroup.DelayBeforeGroup),
                cancellationToken: token
            );
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
                    cancellationToken : token
                );
            }
        }
    }

    private BaseMonster SpawnMonster(
        SpawnGroupData spawnGroup,
        Portal portal
    )
    {
        BaseMonster monster = Instantiate (
            spawnGroup.MonsterPrefab,
            portal.SpawnPoint.position,
            portal.SpawnPoint.rotation,
            transform
        );

        monster.Setup(
            spawnGroup.MonsterData,
            portal.GroundPath,
            portal.MainCastle
        );

        return monster;
    }
}
