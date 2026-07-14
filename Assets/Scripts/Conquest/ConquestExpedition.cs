using UnityEngine;

public class ConquestExpedition
{
    //  **비용 지불** — 인원 할당 + 재료 (말·나무·돌 등)
    public Vector2Int TargetChunkCoord { get; }
    public int DaysRequired { get; }
    public int DaysProgressed { get; private set; }
    public bool IsComplete => DaysProgressed >= DaysRequired;

    public ConquestExpedition(Vector2Int targetChunkCoord, int daysRequired)
    {
        TargetChunkCoord = targetChunkCoord;
        DaysRequired = daysRequired;
    }

    public void AdvanceDay()
    {
        DaysProgressed++;
    }
}
