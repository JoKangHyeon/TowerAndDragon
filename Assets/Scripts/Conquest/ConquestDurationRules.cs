using UnityEngine;

public static class ConquestDurationRules
{
    public static int ResolveDaysRequired(
        Vector2Int targetChunkCoord,
        Vector2Int originChunkCoord,
        int baseDaysAtInnerRing,
        int daysPerDistanceStep,
        int externalDaysReduction,
        int minimumDaysRequired)
    {
        int orthogonalDistance = CalculateOrthogonalDistance(targetChunkCoord, originChunkCoord);
        int distancePastInnerGrass = Mathf.Max(0, orthogonalDistance - 1);
        int daysBeforeReduction = Mathf.Max(
            minimumDaysRequired,
            Mathf.Max(0, baseDaysAtInnerRing) + distancePastInnerGrass * Mathf.Max(0, daysPerDistanceStep));

        return Mathf.Max(minimumDaysRequired, daysBeforeReduction - Mathf.Max(0, externalDaysReduction));
    }

    public static int CalculateOrthogonalDistance(
        Vector2Int targetChunkCoord,
        Vector2Int originChunkCoord)
    {
        Vector2Int delta = targetChunkCoord - originChunkCoord;
        return Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
    }
}
