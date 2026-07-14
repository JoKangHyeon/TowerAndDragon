using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 번의 밤에 실행할 포탈별 적 편성 전체를 정의한다.
/// 일차, 주기와 난이도에 따른 웨이브 선택은 별도 진행 시스템이 담당한다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Wave/Wave Definition", fileName = "WaveDefinition")]
public class WaveDefinitionSO : ScriptableObject
{
    [Tooltip("이번 웨이브에서 실행할 포탈별 적 편성입니다.")]
    [SerializeField] private List<PortalWaveData> _portalWaves;

    public IReadOnlyList<PortalWaveData> PortalWaves => _portalWaves;

    private void OnValidate()
    {
        if (_portalWaves == null)
        {
            Debug.LogError("WaveDefinitionSO: 포탈 편성 목록이 없습니다.", this);
            return;
        }

        HashSet<PortalDirection> portalIds = new HashSet<PortalDirection>();

        foreach (PortalWaveData portalWave in _portalWaves)
        {
            if (portalWave == null)
            {
                Debug.LogError("WaveDefinitionSO: 비어 있는 포탈 편성이 있습니다.", this);
                continue;
            }

            ValidatePortalWave(portalWave, portalIds);
        }
    }

    private void ValidatePortalWave(PortalWaveData portalWave, HashSet<PortalDirection> portalIds)
    {
        if (portalWave.PortalDirectionId == PortalDirection.None)
        {
            Debug.LogError("WaveDefinitionSO: 포탈 편성의 PortalId가 None입니다.", this);
        }
        else if (!portalIds.Add(portalWave.PortalDirectionId))
        {
            Debug.LogError($"WaveDefinitionSO: {portalWave.PortalDirectionId} 포탈 편성이 중복되었습니다.", this);
        }

        if (portalWave.StartDelay < 0)
        {
            Debug.LogError($"WaveDefinitionSO: {portalWave.PortalDirectionId} 포탈의 시작 지연은 음수일 수 없습니다.", this);
        }

        if (portalWave.SpawnGroups == null)
        {
            Debug.LogError($"WaveDefinitionSO: {portalWave.PortalDirectionId} 포탈의 생성 그룹 목록이 없습니다.", this);
            return;
        }

        foreach (SpawnGroupData spawnGroup in portalWave.SpawnGroups)
        {
            ValidateSpawnGroup(portalWave.PortalDirectionId, spawnGroup);
        }
    }

    private void ValidateSpawnGroup(PortalDirection portalId, SpawnGroupData spawnGroup)
    {
        if (spawnGroup == null)
        {
            Debug.LogError($"WaveDefinitionSO: {portalId} 포탈에 비어 있는 생성 그룹이 있습니다.", this);
            return;
        }

        if (spawnGroup.MonsterPrefab == null)
        {
            Debug.LogError($"WaveDefinitionSO: {portalId} 포탈 생성 그룹에 적 프리팹이 없습니다.", this);
        }

        if (spawnGroup.MonsterData == null)
        {
            Debug.LogError($"WaveDefinitionSO: {portalId} 포탈 생성 그룹에 적 데이터가 없습니다.", this);
        }

        if (spawnGroup.SpawnCount <= 0)
        {
            Debug.LogError($"WaveDefinitionSO: {portalId} 포탈 생성 그룹의 생성 수는 0보다 커야 합니다.", this);
        }

        if (spawnGroup.SpawnInterval < 0)
        {
            Debug.LogError($"WaveDefinitionSO: {portalId} 포탈 생성 그룹의 생성 간격은 음수일 수 없습니다.", this);
        }

        if (spawnGroup.DelayBeforeGroup < 0)
        {
            Debug.LogError($"WaveDefinitionSO: {portalId} 포탈 생성 그룹의 시작 지연은 음수일 수 없습니다.", this);
        }
    }
}
