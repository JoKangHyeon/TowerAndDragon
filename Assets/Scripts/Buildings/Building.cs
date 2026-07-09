using UnityEngine;

public class Building : MonoBehaviour
{
    [SerializeField]
    private Sprite _sprite;

    [SerializeField]
    private int _cellSize = 1;
    public int CellSize => _cellSize;
}
