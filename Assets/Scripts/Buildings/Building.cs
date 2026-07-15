using UnityEngine;

public class Building : MonoBehaviour
{
    [SerializeField]
    private Sprite _sprite;

    [SerializeField]
    private FootprintShape _footprintShape;

    private bool _isOpen = false; // 해금 여부
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private Vector3? _placementOffset;

    public bool IsOpen => _isOpen;
    public Sprite Sprite => _sprite;
    public FootprintShape FootprintShape => _footprintShape;

    public bool IsMoveable;
    public bool IsRemoveable;

    // 배치/재배치 시 footprint 중심에 더할 오프셋 - 재배치시 localposition 더해줄 때 누적됨 방지
    public Vector3 PlacementOffset => _placementOffset ?? transform.localPosition;
    public void SetPlacementOffset(Vector3 offset) => _placementOffset = offset;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
    }

    public void SetHighlighted(bool isHighlighted, Color highlightColor)
    {
        if (_spriteRenderer == null)
            return;

        _spriteRenderer.color = isHighlighted ? highlightColor : _originalColor;
    }
}
