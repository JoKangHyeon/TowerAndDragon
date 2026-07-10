using UnityEngine;
using UnityEngine.InputSystem;

public class MouseSelectController : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    private GridMap _gridMap;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        Vector3 worldPos = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;

        Vector3Int cellCoord = _gridMap.ConvertWorldToGrid(worldPos);
        transform.position = _gridMap.ConvertGridToWorld(cellCoord);

        _spriteRenderer.color = _gridMap.CanConstructBuilding(cellCoord) ? Color.green : Color.red;
    }
}
