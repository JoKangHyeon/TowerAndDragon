using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Portal/Seal Stone Data",
    fileName = "SealStoneData")]
public sealed class SealStoneData : ScriptableObject
{
    [SerializeField] private string _nameLocKey;

    [Tooltip("몇 차 연구로 해금되는 봉인석인지(1~4) - 연구 해금 조회(ISealStoneUnlockQuery)의 키로 쓰인다.")]
    [SerializeField] private int _order;

    [SerializeField] private ResourceAmount[] _buildCost;

    public string NameLocKey => _nameLocKey;
    public int Order => _order;
    public IReadOnlyList<ResourceAmount> BuildCost =>
        _buildCost ?? System.Array.Empty<ResourceAmount>();
}
