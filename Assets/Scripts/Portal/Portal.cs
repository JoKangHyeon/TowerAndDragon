using UnityEngine;
using UnityEngine.Splines;

public class Portal : MonoBehaviour
{
    [SerializeField] private PortalId _id;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private SplineContainer _groundPath;
    [SerializeField] private Transform _mainCastle;
    [SerializeField] private bool _isActive;
    //[SerializeField] private bool _isConfigured;


    public PortalId Id => _id;
    public Transform SpawnPoint => _spawnPoint;

    // 지상적일 경우 사용할 경로
    public SplineContainer GroundPath => _groundPath;
    public Transform MainCastle => _mainCastle ;
    public bool IsActive => _isActive;
    public bool IsConfigured => 
        _id != PortalId.None &&
        _spawnPoint != null &&
        _groundPath != null &&
        _mainCastle != null;

}
