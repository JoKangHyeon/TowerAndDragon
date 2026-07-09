using UnityEngine;
using UnityEngine.InputSystem;

public class BuildingSpawnDebugger : MonoBehaviour
{
    [SerializeField]
    private GridMap _gridMap;

    private void Update()
    {
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            Debug.Log($"[BuildingSpawnDebugger] 1번 눌림 -> 타워 건설 시도");
            TrySpawn<Tower>();
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            Debug.Log($"[BuildingSpawnDebugger] 2번 눌림 -> 기본 건물 건설 시도");
            TrySpawn<Building>();
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            Debug.Log($"[BuildingSpawnDebugger] 3번 눌림 -> 공장 건설 시도");
            TrySpawn<Factory>();
        }
    }

    private void TrySpawn<T>() where T : Building
    {
        if (_gridMap.TryGetRandomValidCoord<T>(out Vector3Int coord))
        {
            Debug.Log($"[BuildlingSpawnDebugger] 건설 위치: {coord}");
            _gridMap.ConstructBuilding<T>(coord);
        }
    }
}
