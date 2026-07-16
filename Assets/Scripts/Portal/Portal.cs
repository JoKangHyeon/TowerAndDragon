using UnityEngine;
using UnityEngine.Splines;

public class Portal : MonoBehaviour
{
    [SerializeField] private PortalDirection _portalDirectionId;
    [SerializeField] private TerrainType _terrainType;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private SplineContainer _groundPath;
    [SerializeField] private Transform _mainCastle;
    [SerializeField] private bool _isActive;
    //[SerializeField] private bool _isConfigured;


    public PortalDirection PortalDirectionId => _portalDirectionId;
    public TerrainType TerrrainType => _terrainType;
    public Transform SpawnPoint => _spawnPoint;

    // 지상적일 경우 사용할 경로
    public SplineContainer GroundPath => _groundPath;
    public Transform MainCastle => _mainCastle;
    public bool IsActive => _isActive;
    public bool IsConfigured => 
        _portalDirectionId != PortalDirection.None &&
        _spawnPoint != null &&
        _groundPath != null &&
        _mainCastle != null;

}
