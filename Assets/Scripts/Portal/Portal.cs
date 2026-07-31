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

    private bool _isSealed;

    public PortalDirection PortalDirectionId => _portalDirectionId;
    public TerrainType TerrainType => _terrainType;
    public Transform SpawnPoint => _spawnPoint;

    // 지상적일 경우 사용할 경로
    public SplineContainer GroundPath => _groundPath;
    public Transform MainCastle => _mainCastle;
    public bool IsActive => _isActive;
    public bool IsSealed => _isSealed;
    public bool IsConfigured =>
        _portalDirectionId != PortalDirection.None &&
        _spawnPoint != null &&
        _groundPath != null &&
        _mainCastle != null;

    public void SetSpawnActive(bool isActive)
    {
        _isActive = isActive;
    }

    // 4포탈 봉인석이 전부 지어지는 순간 PortalSealManager가 4개 전부에 호출한다.
    // 봉인되는 즉시 스폰도 멈춘다 - 다음 주기 시작(PortalProgressionController.ApplyCycle)을 기다릴 필요 없다.
    public void Seal()
    {
        _isSealed = true;
        _isActive = false;
    }
}
