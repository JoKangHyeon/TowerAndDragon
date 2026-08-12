using UnityEngine;

// 어떤 셀이 몬스터의 진격 경로 위인지 조회한다.
// 임시 장애물(암석 액티브의 방벽)은 경로를 막는 물건이므로 경로 위에만 설치할 수 있어야 한다.
public interface IMonsterPathQuery
{
    bool IsOnMonsterPath(Vector3Int coord);
}
