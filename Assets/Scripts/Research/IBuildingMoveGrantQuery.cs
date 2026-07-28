public interface IBuildingMoveGrantQuery
{
    bool HasRemainingMoveGrant { get; }
    void ConsumeMoveGrant();
}
