using UnityEngine;
using UnityEngine.InputSystem;

public class MouseSelectController : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    private GridMap _gridMap;

    [SerializeField]
    private float _yOffset = 0.7f;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
    }

    private void Update()
    {
        Vector3 worldPos = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;
        worldPos.y -= _yOffset;

        Vector3Int cellCoord = _gridMap.ConvertWorldToGrid(worldPos);
        var currentPos = _gridMap.ConvertGridToWorld(cellCoord);
        currentPos.y += _yOffset;
        transform.position = currentPos;

        _spriteRenderer.color = _gridMap.CanConstructBuilding(cellCoord) ? Color.green : Color.red;
    }
}
