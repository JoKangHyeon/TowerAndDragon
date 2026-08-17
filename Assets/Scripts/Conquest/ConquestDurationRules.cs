using UnityEngine;

public static class ConquestDurationRules
{
    public static int ResolveDaysRequired(
        int baseDaysRequired,
        int externalDaysReduction,
        Vector2Int targetChunkCoord,
        Vector2Int originChunkCoord,
        int nearOriginDistanceThreshold,
        int nearOriginDaysReduction,
        int minimumDaysRequired)
    {
        int totalReduction = Mathf.Max(0, externalDaysReduction);

        if (IsWithinOrthogonalDistance(
                targetChunkCoord,
                originChunkCoord,
                nearOriginDistanceThreshold))
        {
            totalReduction += Mathf.Max(0, nearOriginDaysReduction);
        }

        return Mathf.Max(
            minimumDaysRequired,
            baseDaysRequired - totalReduction);
    }

    private static bool IsWithinOrthogonalDistance(
        Vector2Int targetChunkCoord,
        Vector2Int originChunkCoord,
        int distanceThreshold)
    {
        if (distanceThreshold <= 0)
        {
            return false;
        }

        Vector2Int delta = targetChunkCoord - originChunkCoord;
        int distance = Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        return distance <= distanceThreshold;
    }
}
