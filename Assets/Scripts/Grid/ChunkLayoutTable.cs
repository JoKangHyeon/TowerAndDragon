using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>셀이 어느 청크에 속하는지를 수기로 지정하는 레이아웃 테이블.
/// 청크를 9×9 정사각형이 아닌 임의 모양으로 정의할 수 있게 하는 것이 이 에셋의 전부이며,
/// 청크의 내용(지형·자원·생산량)은 여전히 타일맵과 다른 테이블들이 정한다.
///
/// ChunkYieldTable·ConquestChunkCostTable과 같은 "좌표별 엔트리 테이블" 패턴이고,
/// GridMap이 넘긴 컬렉션을 테이블이 채우는 계약은 ResourceNodeAreaTable.ApplyToGrid와 같다.
/// 엔트리는 ChunkLayoutEditorWindow가 굽거나 씬뷰 브러시로 칠해서 만든다.</summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Chunk Layout Table")]
public class ChunkLayoutTable : ScriptableObject
{
    [Serializable]
    private struct Entry
    {
        public Vector2Int ChunkCoord;

        [Tooltip("이 청크에 속한 셀 좌표. 셀 좌표의 z는 항상 0이므로 저장하지 않는다.")]
        public Vector2Int[] CellCoords;
    }

    [SerializeField] private Entry[] _entries;

    public int ChunkCount => _entries != null ? _entries.Length : 0;

    /// <summary>셀→청크 역인덱스를 채운다(GridMap.GenerateChunks가 호출).
    /// 한 셀이 여러 청크에 지정돼 있으면 <b>먼저 나온 엔트리가 이긴다</b> - 뒤에 나온 쪽이 이기게 하면
    /// 배열 순서를 바꾸는 것만으로 소속이 조용히 뒤집힌다. 중복은 경고로 드러낸다
    /// (ChunkLayoutEditorWindow의 검증 기능이 같은 중복을 미리 잡아준다).</summary>
    public void BuildCellToChunkIndex(Dictionary<Vector3Int, Vector2Int> result)
    {
        result.Clear();

        if (_entries == null)
            return;

        int duplicateCount = 0;

        foreach (Entry entry in _entries)
        {
            if (entry.CellCoords == null)
                continue;

            foreach (Vector2Int cellCoord in entry.CellCoords)
            {
                var coord = new Vector3Int(cellCoord.x, cellCoord.y, 0);

                if (result.TryGetValue(coord, out Vector2Int ownerChunkCoord))
                {
                    duplicateCount++;
                    Debug.LogWarning(
                        $"[ChunkLayoutTable] 셀 {cellCoord}이(가) 청크 {ownerChunkCoord}와 {entry.ChunkCoord}에 " +
                        $"중복 지정되었습니다 - 먼저 나온 {ownerChunkCoord}를 유지합니다.", this);
                    continue;
                }

                result[coord] = entry.ChunkCoord;
            }
        }

        if (duplicateCount > 0)
            Debug.LogError($"[ChunkLayoutTable] 중복 지정된 셀 {duplicateCount}개 - 레이아웃 에셋을 확인하세요.", this);
    }
}
