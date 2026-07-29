using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Move Allowance",
    fileName = "MoveAllowanceEffect")]
public sealed class MoveAllowanceEffectSO : ResearchEffectSO
{
    [Min(0)]
    [SerializeField] private int _moveAllowance;

    public int MoveAllowance => _moveAllowance;

    public override int GetMoveAllowanceBonus()
    {
        return _moveAllowance;
    }
}
