using System.Collections.Generic;
using UnityEngine;

/// <summary>한 점에 겹친 건물 후보를 모아, 같은 자리를 다시 누를 때마다 다음 후보로 돌린다.
/// 후보는 지면 셀 점유 건물과 스프라이트 몸통에 걸린 건물의 합집합이며, 화면 앞쪽부터 나열한다 -
/// 첫 클릭은 언제나 눈에 보이는 것을 고르므로 겹치지 않은 건물의 조작감은 그대로다.
///
/// 성처럼 스프라이트가 자기 풋프린트보다 훨씬 높게 그려진 건물은 몸통 아래의 지면 셀이 빈 땅이라,
/// 지면 셀만 보던 예전 판정으로는 몸통을 눌러도 아무것도 잡히지 않거나 성이 뒤 건물의 클릭을
/// 가로챘다. 몸통 판정을 모든 건물로 넓히고 그 결과를 순환시켜 두 문제를 함께 없앤다.</summary>
public sealed class BuildingClickCycle
{
    private readonly GridMap _gridMap;

    // 확정된 순환 목록. 같은 자리를 다시 누르는 동안 이 목록과 인덱스가 유지된다.
    private readonly List<Building> _candidates = new();

    // 후보 수집용 재사용 버퍼 - 클릭마다 새로 할당하지 않는다.
    private readonly List<Building> _collectBuffer = new();

    private int _index;
    private Vector2 _anchorScreenPosition;
    private bool _hasAnchor;

    public BuildingClickCycle(GridMap gridMap)
    {
        _gridMap = gridMap;
    }

    /// <summary>이번 클릭이 고를 건물. 후보가 없으면 null.
    /// 직전 클릭과 같은 자리(스크린 거리 sameClickRadius 이내)이고 후보 목록이 그대로면 다음 후보로 넘긴다.</summary>
    public Building ResolveNext(
        Vector3 pointerWorldPoint,
        Vector3Int groundCell,
        Vector2 pointerScreenPosition,
        float sameClickRadius)
    {
        Collect(pointerWorldPoint, groundCell);

        if (_collectBuffer.Count == 0)
        {
            Reset();
            return null;
        }

        bool continuesCycle = _hasAnchor
            && Vector2.Distance(_anchorScreenPosition, pointerScreenPosition) <= sameClickRadius
            && HasSameCandidates();

        if (continuesCycle)
        {
            _index = (_index + 1) % _candidates.Count;
        }
        else
        {
            _candidates.Clear();
            _candidates.AddRange(_collectBuffer);
            _index = 0;
        }

        // 앵커를 매 클릭 갱신한다 - 고정해 두면 커서가 조금씩 흘러가는 것만으로 순환이 끊긴다.
        _anchorScreenPosition = pointerScreenPosition;
        _hasAnchor = true;

        return _candidates[_index];
    }

    /// <summary>순환을 처음부터 다시 시작하게 한다. 클릭이 아닌 경로로 선택이 바뀌었을 때 부른다.</summary>
    public void Reset()
    {
        _candidates.Clear();
        _collectBuffer.Clear();
        _index = 0;
        _hasAnchor = false;
    }

    private void Collect(Vector3 pointerWorldPoint, Vector3Int groundCell)
    {
        _collectBuffer.Clear();

        // 지면 셀 점유 건물을 먼저 담아 둔다 - 아래 정렬에서 동점이 나도 예전 기준이 살아 있도록.
        Building occupant = _gridMap.GetBuildingAt(groundCell);

        // IsClickSelectable이 false인 건물(성)은 후보에서 뺀다 - 순환 목록에 남겨 두면 그 건물에
        // 가려진 뒤쪽 건물을 고르려고 같은 자리를 한 번 더 눌러야 한다.
        if (occupant != null && occupant.IsClickSelectable)
            _collectBuffer.Add(occupant);

        foreach (Building building in _gridMap.Buildings)
        {
            if (building == null || building == occupant || !building.IsClickSelectable)
                continue;

            if (building.ContainsWorldPoint(pointerWorldPoint))
                _collectBuffer.Add(building);
        }

        // 정렬 규칙은 Building이 들고 있다 - 호버 아웃라인도 같은 비교자를 써서
        // "눌리는 것"과 "아웃라인이 뜨는 것"을 일치시킨다.
        _collectBuffer.Sort(Building.CompareFrontToBack);
    }

    private bool HasSameCandidates()
    {
        if (_candidates.Count != _collectBuffer.Count)
            return false;

        for (int i = 0; i < _candidates.Count; i++)
        {
            // Unity의 ==를 쓴다 - 그 사이 파괴된 건물은 null로 비교돼 자동으로 불일치가 된다.
            if (_candidates[i] != _collectBuffer[i])
                return false;
        }

        return true;
    }
}
