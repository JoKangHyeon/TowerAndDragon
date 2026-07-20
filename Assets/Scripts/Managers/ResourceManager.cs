using System;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    [SerializeField, Min(0)] private int _food;
    [SerializeField, Min(0)] private int _wood;
    [SerializeField, Min(0)] private int _stone;
    [SerializeField, Min(0)] private int _ore;

    public int Food => _food;

    public bool TryGetAmount (ResourceType resourceType, out int amount)
    {
        switch (resourceType)
        {
            case ResourceType.Food:
                amount = _food;
                return true;

            case ResourceType.Wood:
                amount = _wood;
                return true;
              
            case ResourceType.Stone:
                amount = _stone;
                return true;
            
            case ResourceType.Ore:
                amount = _ore;
                return true;

            default:
                amount = 0;
                return false;
        }
    }

    public bool TryAdd(
        ResourceType resourceType,
        int amount
    )
    {
        if (amount <= 0)
        {
            return false;
        }

        if (!TryGetAmount(resourceType, out int currentAmount))
        {
            return false;
        }

        if (currentAmount > int.MaxValue - amount)
        {
            return false;
        }

        return TrySetAmount(resourceType, currentAmount + amount);
    }

    public int ConsumeUpTo(
        ResourceType resourceType,
        int requestedAmount
    )
    {
        if (requestedAmount <= 0)
        {
            return 0;
        }

        if (!TryGetAmount(resourceType, out int currentAmount))
        {
            return 0;
        }

        int consumedAmount = Math.Min(
                currentAmount,
                requestedAmount
        );

        TrySetAmount(resourceType, currentAmount - consumedAmount);

        return consumedAmount;

        
    }

    private bool TrySetAmount(ResourceType resourceType, int amount)
    {
        if (amount < 0)
        {
            return false;
        }   

        switch (resourceType)
        {
            case ResourceType.Food:
                _food = amount;
                return true;

            case ResourceType.Wood:
                _wood = amount;
                return true;
              
            case ResourceType.Stone:
                _stone = amount;
                return true;
            
            case ResourceType.Ore:
                _ore = amount;
                return true;

            default:
                amount = 0;
                return false;
        }
    }
}
