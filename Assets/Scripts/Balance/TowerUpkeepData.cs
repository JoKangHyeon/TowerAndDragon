using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타워 종류별 하루 유지비. 배치 인구 1명당 소모하는 자원을 정의한다
/// (수치 근거는 Docs/자원밸런싱_초안.md §4.2).
///
/// 지역 유지비(TerrainPenaltyTable)가 "어느 땅에 지었는가"로 걷는다면, 이쪽은
/// "무엇을 지었는가"로 걷는다. 둘 다 미납 시 인구를 회수해 비활성화한다(파괴하지 않는다).
///
/// 표에 없는 타워는 유지비가 0이다 - 인구로 가동하지 않는 새끼용 타워처럼
/// 애초에 배치 인구가 없는 것들은 등록할 필요가 없다.
/// </summary>
[CreateAssetMenu(
    menuName = "TowerAndDragon/Balance/TowerUpkeep",
    fileName = "TU_TowerUpkeep")]
public sealed class TowerUpkeepData : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("유지비를 낼 타워 종류.")]
        public TowerData Tower;

        [Tooltip("배치 인구 1명이 하루에 소모하는 자원. 여러 종류를 함께 걸 수 있다.")]
        public ResourceAmount[] UpkeepPerPopulation;
    }

    [SerializeField] private Entry[] _entries;

    /// <summary>
    /// 해당 타워의 인구 1명당 유지비. 표에 없으면 빈 목록을 돌려준다(유지비 없음).
    /// 반환된 배열은 이 에셋이 소유한다 - 호출부가 수정하면 안 된다.
    /// </summary>
    public IReadOnlyList<ResourceAmount> GetUpkeepPerPopulation(TowerData tower)
    {
        if (tower == null || _entries == null)
        {
            return Array.Empty<ResourceAmount>();
        }

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i].Tower == tower)
            {
                return _entries[i].UpkeepPerPopulation ?? Array.Empty<ResourceAmount>();
            }
        }

        return Array.Empty<ResourceAmount>();
    }
}
