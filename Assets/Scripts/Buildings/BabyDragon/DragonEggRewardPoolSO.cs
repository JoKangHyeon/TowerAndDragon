using System;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Dragon Egg Reward Pool",
    fileName = "DragonEggRewardPool")]
public sealed class DragonEggRewardPoolSO : ScriptableObject
{
    private const int MIN_WEIGHT = 1;

    [Serializable]
    private struct Entry
    {
        public DragonType DragonType;

        [Min(MIN_WEIGHT)]
        public int Weight;
    }

    [SerializeField] private Entry[] _entries;

    public bool TryRoll(out DragonType dragonType)
    {
        int totalWeight = GetTotalWeight();

        if (totalWeight <= 0)
        {
            dragonType = default;
            return false;
        }

        int roll = UnityEngine.Random.Range(0, totalWeight);
        return TryResolveRoll(roll, out dragonType);
    }

    public bool TryResolveRoll(int roll, out DragonType dragonType)
    {
        int totalWeight = GetTotalWeight();

        if (roll < 0 || roll >= totalWeight)
        {
            dragonType = default;
            return false;
        }

        foreach (Entry entry in _entries)
        {
            if (entry.Weight < MIN_WEIGHT)
            {
                continue;
            }

            if (roll < entry.Weight)
            {
                dragonType = entry.DragonType;
                return true;
            }

            roll -= entry.Weight;
        }

        dragonType = default;
        return false;
    }

    private int GetTotalWeight()
    {
        if (_entries == null)
        {
            return 0;
        }

        int totalWeight = 0;

        foreach (Entry entry in _entries)
        {
            if (entry.Weight >= MIN_WEIGHT)
            {
                totalWeight += entry.Weight;
            }
        }

        return totalWeight;
    }
}
