// 건물 하나에 걸린 효과 한 개의 표시용 형태. 표식(아이콘 + 수치)과 툴팁이 함께 쓴다.
//
// 유지비 계열(나무·돌)과 감소율 계열(생산량·공격속도)은 단위가 달라 한 필드에 담을 수 없다.
// 해당하지 않는 쪽은 0으로 남기고, 어느 쪽을 읽을지는 Kind가 정한다
// (BuildingEffectFormatter가 이 분기를 한 곳에서 처리한다).
public readonly struct BuildingEffectDescriptor
{
    public BuildingEffectKind Kind { get; }

    /// <summary>다음 정산에서 실제로 나갈 하루치 소모량. 유지비 계열에만 값이 있다.</summary>
    public int DailyAmount { get; }

    /// <summary>깎이는 비율(0.3이면 -30%). 감소율 계열에만 값이 있다.</summary>
    public float Ratio { get; }

    private BuildingEffectDescriptor(BuildingEffectKind kind, int dailyAmount, float ratio)
    {
        Kind = kind;
        DailyAmount = dailyAmount;
        Ratio = ratio;
    }

    /// <summary>수치가 따라붙지 않는 상태 표식(운영 중단 등).</summary>
    public static BuildingEffectDescriptor Flag(BuildingEffectKind kind) =>
        new BuildingEffectDescriptor(kind, 0, 0f);

    public static BuildingEffectDescriptor Upkeep(BuildingEffectKind kind, int dailyAmount) =>
        new BuildingEffectDescriptor(kind, dailyAmount, 0f);

    public static BuildingEffectDescriptor Reduction(BuildingEffectKind kind, float ratio) =>
        new BuildingEffectDescriptor(kind, 0, ratio);
}
