using UnityEngine;

// 타워 스탯 연구 효과가 "어떤 타워에 걸리는가"를 정하는 필터.
// TowerDamageMultiplierEffectSO · TowerRangeMultiplierEffectSO · TowerAttackSpeedMultiplierEffectSO가
// 같은 판정을 쓰므로 여기 한 곳에 모았다 - 셋에 각각 복사하면 판정이 갈라진다.
//
// 기본값(All)이 필터 도입 전 동작이다. 이미 저작된 에셋은 이 필드가 0으로 읽혀
// "종류를 가리지 않고 전 타워"가 그대로 유지된다.
[System.Serializable]
public struct TowerTargetFilter
{
    public enum FilterMode
    {
        All,             // 종류를 가리지 않는다(필터 도입 전 동작)
        ElementalTowers, // 속성 타워 전부
        MatchingElement, // 지정한 속성의 속성 타워만
        MatchingCategory, // 지정한 타워 카테고리만
    }

    [Tooltip("이 효과를 어떤 타워에 걸지. All이 기본이며 전 타워에 적용한다.")]
    [SerializeField] private FilterMode _mode;

    [Tooltip("MatchingElement일 때만 쓰인다.")]
    [SerializeField] private DragonType _element;

    [Tooltip("MatchingCategory일 때만 쓰인다.")]
    [SerializeField] private TowerCategory _category;

    public FilterMode Mode => _mode;
    public DragonType Element => _element;
    public TowerCategory Category => _category;

    /// <summary>
    /// 이 타워가 대상인가.
    ///
    /// 속성 판정을 IElementalAttackData가 아니라 ElementalTowerData로 하는 이유:
    /// 새끼용(BabyDragonData)도 그 인터페이스를 구현하므로 인터페이스로 재면 "속성 타워 강화" 연구가
    /// 새끼용까지 같이 올려 준다. 새끼용 강화는 용 스킬트리의 새끼용 갈래가 담당하므로
    /// 두 트리가 같은 대상에 이중으로 얹히지 않게 여기서 갈라 둔다.
    /// </summary>
    public bool Matches(TowerData towerData)
    {
        if (_mode == FilterMode.All)
        {
            return true;
        }

        if (_mode == FilterMode.MatchingCategory)
        {
            return towerData != null && towerData.Category == _category;
        }

        if (towerData is not ElementalTowerData elemental)
        {
            return false;
        }

        return _mode == FilterMode.ElementalTowers || elemental.DragonType == _element;
    }
}
