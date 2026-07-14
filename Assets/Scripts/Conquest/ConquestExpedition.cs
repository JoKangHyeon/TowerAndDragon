using UnityEngine;

public class ConquestExpedition
{
    public Vector2Int TargetChunkCoord { get; }
    public ResourceCost Cost { get; }
    public int DaysRequired { get; }
    public int DaysProgressed { get; private set; }
    public bool IsComplete => DaysProgressed >= DaysRequired;

    public ConquestExpedition(Vector2Int targetChunkCoord, ResourceCost cost, int daysRequired)
    {
        TargetChunkCoord = targetChunkCoord;
        Cost = cost;
        DaysRequired = daysRequired;
    }

    public void AdvanceDay()
    {
        DaysProgressed++;
    }
}
