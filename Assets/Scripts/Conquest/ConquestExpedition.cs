using UnityEngine;

public class ConquestExpedition
{
    public Vector2Int TargetChunkCoord { get; }
    public ResourceCost Cost { get; }
    public int DaysRequired { get; }
    public int DaysProgressed { get; private set; }
    public bool IsComplete => DaysProgressed >= DaysRequired;
    public float Progress => (float)DaysProgressed / DaysRequired;

    public ConquestExpedition(Vector2Int targetChunkCoord, ResourceCost cost, int daysRequired)
    {
        TargetChunkCoord = targetChunkCoord;
        Cost = cost;
        DaysRequired = daysRequired;
    }

    // 세이브 복원 전용. DaysProgressed가 private set이라 AdvanceDay를 반복 호출하지 않고
    // 진행도를 직접 세운다.
    public ConquestExpedition(
        Vector2Int targetChunkCoord,
        ResourceCost cost,
        int daysRequired,
        int daysProgressed)
        : this(targetChunkCoord, cost, daysRequired)
    {
        DaysProgressed = daysProgressed;
    }

    public void AdvanceDay()
    {
        DaysProgressed++;
    }
}
