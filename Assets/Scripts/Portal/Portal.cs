using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class Portal : MonoBehaviour
{
    [SerializeField] private PortalDirection _portalDirectionId;
    [SerializeField] private TerrainType _terrainType;
    [SerializeField] private Transform _spawnPoint;

    // 지상적이 사용할 경로(루트) 목록. 같은 포탈에서 같은 성으로 가는 대체 경로들이며,
    // 웨이브 데이터의 RouteWaveData.RouteIndex가 이 목록의 인덱스를 가리킨다.
    [SerializeField] private List<SplineContainer> _groundPaths;

    [SerializeField] private Transform _mainCastle;
    [SerializeField] private bool _isActive;

    private bool _isSealed;

    public PortalDirection PortalDirectionId => _portalDirectionId;
    public TerrainType TerrainType => _terrainType;
    public Transform SpawnPoint => _spawnPoint;

    public IReadOnlyList<SplineContainer> GroundPaths => _groundPaths;
    public int RouteCount => _groundPaths != null ? _groundPaths.Count : 0;

    public Transform MainCastle => _mainCastle;
    public bool IsActive => _isActive;
    public bool IsSealed => _isSealed;
    public bool IsConfigured =>
        _portalDirectionId != PortalDirection.None &&
        _spawnPoint != null &&
        HasValidGroundPaths &&
        _mainCastle != null;

    public bool TryGetGroundPath(int routeIndex, out SplineContainer path)
    {
        if (routeIndex < 0 || routeIndex >= RouteCount)
        {
            path = null;
            return false;
        }

        path = _groundPaths[routeIndex];
        return path != null;
    }

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

    private bool HasValidGroundPaths
    {
        get
        {
            if (RouteCount == 0)
            {
                return false;
            }

            foreach (SplineContainer path in _groundPaths)
            {
                if (path == null)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
