using System.Collections.Generic;
using UnityEngine;

public class PortalWaveData : MonoBehaviour
{
    [SerializeField] private PortalId _portalId;
    [SerializeField] private float _startDay;
    [SerializeField] private List<SpawnGroupData> _spawnGroups;

    public PortalId PortaId => _portalId;
    public float StartDay => _startDay;
    public IReadOnlyList<SpawnGroupData> SpawnGroup => _spawnGroups;
}
