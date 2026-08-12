// 보상 지급(LandmarkRewardSO.Grant)에 필요한 참조 묶음.
// ProgressionContext와 같은 의도 - 파생 보상이 필요한 시스템만 골라 쓴다.
// 필드를 늘릴 때 모든 파생 보상의 시그니처를 건드리지 않아도 되도록 묶어서 넘긴다.
public sealed class LandmarkGrantContext
{
    public LandmarkDataSO Data { get; }
    public DragonEggInventorySystem EggInventory { get; }
    public ResourceManager Resources { get; }

    public LandmarkGrantContext(
        LandmarkDataSO data,
        DragonEggInventorySystem eggInventory,
        ResourceManager resources)
    {
        Data = data;
        EggInventory = eggInventory;
        Resources = resources;
    }
}
