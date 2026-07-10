using UnityEngine;

public class Building : MonoBehaviour
{
    [SerializeField]
    private Sprite _sprite;

    [SerializeField]
    private FootprintShape _footprintShape;

    public Sprite Sprite => _sprite;
    public FootprintShape FootprintShape => _footprintShape;
}
