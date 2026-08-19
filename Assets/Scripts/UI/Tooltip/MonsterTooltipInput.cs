using System.Collections.Generic;

/// <summary>
/// 몬스터 툴팁 한 장을 그리는 데 필요한 것을 모은 값. 낮의 출현 예고 카드와 밤의 개체 호버가
/// 같은 빌더를 쓰게 하려고 둔다 - 두 화면의 차이는 "살아 있는 개체가 있는가" 하나뿐이고,
/// 그 차이를 여기서 흡수해 MonsterTooltipBuilder에는 분기가 남지 않게 한다.
///
/// 상태 줄은 이 값이 만들지 않고 호출자가 채워 넣는다. 출처(개체 상태이상 · 전역 강화)를
/// 아는 쪽이 서로 다르고, 앞으로 하드모드 전역 버프 같은 새 출처가 붙을 자리이기 때문이다.
/// </summary>
public readonly struct MonsterTooltipInput
{
    public MonsterData Data { get; }

    /// <summary>살아 있는 개체. 예고 카드처럼 개체가 없는 화면에서는 null이다.</summary>
    public BaseMonster Instance { get; }

    public EnemyEnhancementSnapshot Enhancement { get; }

    public IReadOnlyList<MonsterStatusLine> StatusLines { get; }

    // Unity의 == 오버로드를 쓴다 - 툴팁을 띄운 채 적이 죽으면 파괴된 개체가 남아 있는데,
    // 패턴 매칭(is not null)은 그것을 "null 아님"으로 보고 그대로 역참조한다.
    public bool HasInstance => Instance != null;

    /// <summary>낮의 예고 카드용. 개체는 없고 전역 강화만 반영한다.</summary>
    public static MonsterTooltipInput ForPreview(
        MonsterData data,
        in EnemyEnhancementSnapshot enhancement,
        IReadOnlyList<MonsterStatusLine> statusLines) =>
        new MonsterTooltipInput(data, null, enhancement, statusLines);

    /// <summary>밤의 개체 호버용. 수치는 데이터가 아니라 개체에서 읽는다.</summary>
    public static MonsterTooltipInput ForInstance(
        BaseMonster instance,
        IReadOnlyList<MonsterStatusLine> statusLines) =>
        new MonsterTooltipInput(
            instance != null ? instance.Data : null,
            instance,
            instance != null ? instance.Enhancement : EnemyEnhancementSnapshot.Neutral,
            statusLines);

    private MonsterTooltipInput(
        MonsterData data,
        BaseMonster instance,
        in EnemyEnhancementSnapshot enhancement,
        IReadOnlyList<MonsterStatusLine> statusLines)
    {
        Data = data;
        Instance = instance;
        Enhancement = enhancement;
        StatusLines = statusLines;
    }

    // 최대 체력은 개체가 있으면 개체에서 읽는다. MonsterData.MaxHealth는 강화 이전의 설계값이라,
    // 그대로 쓰면 "현재 체력이 최대치를 넘는" 줄이 나온다.
    public float MaxHealth =>
        HasInstance ? Instance.MaxHealth : Enhancement.MaxHealth.Apply(SafeMaxHealth);

    public float CurrentHealth => HasInstance ? Instance.CurrentHealth : 0f;

    public float MoveSpeed => Enhancement.MoveSpeed.Apply(Data != null ? Data.MoveSpeed : 0f);

    public bool HasShield => Data != null && Data.HasShield;

    public float MaxShield =>
        HasInstance && Instance.Shield != null
            ? Instance.Shield.MaxShield
            : Enhancement.ShieldAmount.Apply(SafeShieldAmount);

    public float CurrentShield =>
        HasInstance && Instance.Shield != null ? Instance.Shield.CurrentShield : 0f;

    // 개체의 방어막은 Setup에서 초기화되므로, 개체가 있어도 아직 컴포넌트가 없으면 데이터로 되돌아간다.
    public bool ShowsCurrentShield => HasInstance && Instance.Shield != null;

    private float SafeMaxHealth => Data != null ? Data.MaxHealth : 0f;
    private float SafeShieldAmount => Data != null ? Data.ShieldAmount : 0f;
}
