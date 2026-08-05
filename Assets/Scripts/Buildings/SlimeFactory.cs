using System.Collections.Generic;
using UnityEngine;

public class SlimeFactory : Factory
{
    public List<SpriteRenderer> slimes;

    private void HandleOnPopulationChanged()
    {
        ShowSlimes();
    }

    public void ShowSlimes()
    {
        
        //            slot.Setup(icon, DragonAttributePalette.TintFor(cost.Type), cost.Amount.ToString(), textColor);

        List<(ResourceType, int)> productedResources = new();

        foreach(var resourceType in EnumerateProducedResourceTypes())
        {
            int resourceAmount = GetCurrentYield(resourceType);

            if (resourceAmount == 0)
                continue;

            productedResources.Add((resourceType, resourceAmount));
        }

        if(productedResources.Count == 0)
        {
            foreach(var renderer in slimes)
            {
                renderer.gameObject.SetActive(false);
            }
            return;
        }

        int slimeIndex = 0;
        int slimeSum = 0;

        for (int i = 0; i < productedResources.Count; i++)
        {
            if (!IsSlime(productedResources[i].Item1))
            {
                continue;
            }

            slimes[slimeIndex].gameObject.SetActive(true);
            slimes[slimeIndex].color = DragonAttributePalette.TintFor(productedResources[i].Item1);
            slimeSum += productedResources[i].Item2;

            slimeIndex++;
            if (slimeIndex >= slimes.Count)
            {
                return;
            }
        }

        int slimeLeft = slimes.Count - slimeIndex;
        Debug.Log(slimeLeft);
        int slimeLeftAfterRatio = slimeLeft;

        List<(ResourceType, int)> assignedSlimes = new();
        List<(ResourceType, float)> leftSlimes = new();

        for (int i = 0; i < productedResources.Count; i++)
        {
            if (!IsSlime(productedResources[i].Item1))
            {
                continue;
            }
            float slimeAssigned = (float)productedResources[i].Item2 / slimeSum * slimeLeft;
            int slimeAssignedInt = Mathf.FloorToInt(slimeAssigned);
            float slimeLeftFloat = slimeAssigned - slimeAssignedInt;

            Debug.Log($"{productedResources[i].Item1} : {slimeAssignedInt} / {slimeLeftFloat}");
            assignedSlimes.Add((productedResources[i].Item1, slimeAssignedInt));
            leftSlimes.Add((productedResources[i].Item1, slimeLeftFloat));

            slimeLeftAfterRatio -= slimeAssignedInt;
        }

        leftSlimes.Sort(((ResourceType, float) a, (ResourceType, float) b) => b.Item2.CompareTo(a.Item2));

        for (int i = 0; i < slimeLeftAfterRatio; i++)
        {
            int index = 0;
            for(int j =0; j< assignedSlimes.Count; j++)
            {
                if (assignedSlimes[j].Item1 == leftSlimes[i].Item1)
                {
                    index = j;
                    break;
                }
            }

            assignedSlimes[index] = (assignedSlimes[index].Item1, assignedSlimes[index].Item2 + 1);
        }

        foreach (var slime in assignedSlimes)
        {
            for(int i = 0; i < slime.Item2; i++)
            {
                slimes[slimeIndex].gameObject.SetActive(true);
                slimes[slimeIndex].color = DragonAttributePalette.TintFor(slime.Item1);
                slimeIndex++;

                if (slimeIndex >= slimes.Count)
                {
                    return;
                }
            }
        }
    }

    private bool IsSlime(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.GrassSlime:
            case ResourceType.RockSlime:
            case ResourceType.VolcanoSlime:
            case ResourceType.DesertSlime:
            case ResourceType.SnowSlime:
                return true;
            default:
                return false;
        }
    }

    public override bool Initialize(ResourceManager resourceManager, CycleManager cycleManager, GridMap gridMap)
    {
        bool result = base.Initialize(resourceManager, cycleManager, gridMap);
        _population.OnPopulationChanged.AddListener(HandleOnPopulationChanged);
        return result;
    }
}
