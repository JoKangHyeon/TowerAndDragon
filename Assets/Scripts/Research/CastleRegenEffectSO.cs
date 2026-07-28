using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Research/Effects/Castle Regen",
    fileName = "CastleRegenEffect")]
public sealed class CastleRegenEffectSO : ResearchEffectSO
{
    [Min(0f)]
    [SerializeField] private float _healPerDay;

    public float HealPerDay => _healPerDay;

    public override float GetCastleDailyRegenAmount()
    {
        return _healPerDay;
    }
}
