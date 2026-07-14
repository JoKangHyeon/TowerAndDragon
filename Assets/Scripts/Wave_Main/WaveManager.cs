using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

/// <summary>
/// 웨이브 실행을 조율한다. 실제 실행 로직은 데이터 계약 검증 후 구현한다.
/// </summary>
public class WaveManager : MonoBehaviour
{
    [SerializeField] private List<Portal> _portals = new();
    private CancellationTokenSource _waveCancellation;
    private readonly Dictionary<PortalId, Portal> _portalById = new();
    private UnityEvent _allSpawnCompleted = new();


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
        
    }

    private BaseMonster SpawnMonster(
        SpawnGroupData spawnGroup,
        Portal portal
    )
    {
        return null;
    }
}
